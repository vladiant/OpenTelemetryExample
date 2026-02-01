"""
Order Service - Handles order creation and management.

This service demonstrates:
- Receiving propagated trace context
- Creating child spans
- Calling downstream services (Inventory)
- Adding custom attributes and events to spans
"""

import os
import logging
import sys
import uuid
import asyncio
from datetime import datetime
from typing import Dict, Any

# Add shared module to path
sys.path.insert(0, "/app/shared")

from fastapi import FastAPI, HTTPException
import httpx
from opentelemetry import trace
from opentelemetry.trace import Status, StatusCode

from tracing import setup_tracing, instrument_fastapi, get_tracer

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Setup tracing
SERVICE_NAME = os.getenv("SERVICE_NAME", "order-service")
setup_tracing(SERVICE_NAME)
tracer = get_tracer(SERVICE_NAME)

# Create FastAPI app
app = FastAPI(
    title="Order Service",
    description="Handles order creation and management",
    version="1.0.0",
)

# Instrument FastAPI
instrument_fastapi(app)

# Service URLs
INVENTORY_SERVICE_URL = os.getenv("INVENTORY_SERVICE_URL", "http://localhost:8002")

# In-memory order storage (for demo purposes)
orders_db: Dict[str, Dict[str, Any]] = {}


@app.get("/")
async def root():
    """Health check endpoint."""
    return {"service": SERVICE_NAME, "status": "healthy"}


@app.get("/health")
async def health():
    """Detailed health check."""
    return {
        "service": SERVICE_NAME,
        "status": "healthy",
        "orders_count": len(orders_db),
    }


@app.post("/orders")
async def create_order(order_data: dict = None):
    """
    Create a new order.

    Steps:
    1. Validate order data
    2. Check inventory availability
    3. Reserve inventory
    4. Create order record
    """
    if order_data is None:
        order_data = {}

    product_id = order_data.get("product_id", "demo-product")
    quantity = order_data.get("quantity", 1)

    # Create span for order processing
    with tracer.start_as_current_span("create_order") as span:
        order_id = str(uuid.uuid4())
        span.set_attribute("order.id", order_id)
        span.set_attribute("order.product_id", product_id)
        span.set_attribute("order.quantity", quantity)

        # Add event for order initiation
        span.add_event(
            "Order processing started",
            {
                "order.id": order_id,
                "timestamp": datetime.utcnow().isoformat(),
            },
        )

        logger.info(f"Creating order {order_id} for product {product_id}")

        # Step 1: Validate order
        with tracer.start_as_current_span("validate_order") as validate_span:
            validate_span.set_attribute("validation.product_id", product_id)
            validate_span.set_attribute("validation.quantity", quantity)

            if quantity <= 0:
                validate_span.set_status(Status(StatusCode.ERROR, "Invalid quantity"))
                raise HTTPException(status_code=400, detail="Quantity must be positive")

            # Simulate validation delay
            await asyncio.sleep(0.05)
            validate_span.add_event("Validation passed")

        # Step 2: Check inventory
        with tracer.start_as_current_span("check_inventory") as inv_span:
            inv_span.set_attribute("inventory.product_id", product_id)
            inv_span.set_attribute("inventory.requested_quantity", quantity)

            try:
                async with httpx.AsyncClient(proxy=None) as client:
                    response = await client.get(
                        f"{INVENTORY_SERVICE_URL}/inventory/{product_id}",
                        timeout=30.0,
                    )

                    if response.status_code == 404:
                        inv_span.set_status(
                            Status(StatusCode.ERROR, "Product not found")
                        )
                        raise HTTPException(status_code=404, detail="Product not found")

                    inventory = response.json()
                    available = inventory.get("quantity", 0)
                    inv_span.set_attribute("inventory.available", available)

                    if available < quantity:
                        inv_span.set_status(
                            Status(StatusCode.ERROR, "Insufficient inventory")
                        )
                        raise HTTPException(
                            status_code=400,
                            detail=f"Insufficient inventory. Available: {available}",
                        )

                    inv_span.add_event(
                        "Inventory check passed",
                        {
                            "available": available,
                            "requested": quantity,
                        },
                    )

            except httpx.RequestError as e:
                inv_span.record_exception(e)
                inv_span.set_status(Status(StatusCode.ERROR, str(e)))
                raise HTTPException(
                    status_code=503, detail="Inventory service unavailable"
                )

        # Step 3: Reserve inventory
        with tracer.start_as_current_span("reserve_inventory") as reserve_span:
            reserve_span.set_attribute("reservation.product_id", product_id)
            reserve_span.set_attribute("reservation.quantity", quantity)

            try:
                async with httpx.AsyncClient(proxy=None) as client:
                    response = await client.post(
                        f"{INVENTORY_SERVICE_URL}/inventory/{product_id}/reserve",
                        json={"quantity": quantity, "order_id": order_id},
                        timeout=30.0,
                    )

                    if response.status_code != 200:
                        reserve_span.set_status(
                            Status(StatusCode.ERROR, "Reservation failed")
                        )
                        raise HTTPException(
                            status_code=response.status_code,
                            detail="Failed to reserve inventory",
                        )

                    reserve_span.add_event("Inventory reserved successfully")

            except httpx.RequestError as e:
                reserve_span.record_exception(e)
                raise HTTPException(
                    status_code=503, detail="Inventory service unavailable"
                )

        # Step 4: Create order record
        with tracer.start_as_current_span("persist_order") as persist_span:
            order = {
                "order_id": order_id,
                "product_id": product_id,
                "quantity": quantity,
                "status": "confirmed",
                "created_at": datetime.utcnow().isoformat(),
            }

            # Simulate database write
            await asyncio.sleep(0.02)
            orders_db[order_id] = order

            persist_span.set_attribute("db.operation", "insert")
            persist_span.set_attribute("db.table", "orders")
            persist_span.add_event("Order persisted to database")

        # Add completion event
        span.add_event(
            "Order processing completed",
            {
                "order.id": order_id,
                "order.status": "confirmed",
            },
        )

        span.set_status(Status(StatusCode.OK))
        logger.info(f"Order {order_id} created successfully")

        return order


@app.get("/orders/{order_id}")
async def get_order(order_id: str):
    """Get order details by ID."""
    with tracer.start_as_current_span("get_order") as span:
        span.set_attribute("order.id", order_id)

        # Simulate database read
        await asyncio.sleep(0.01)

        if order_id not in orders_db:
            span.set_status(Status(StatusCode.ERROR, "Order not found"))
            raise HTTPException(status_code=404, detail="Order not found")

        order = orders_db[order_id]
        span.set_attribute("order.status", order.get("status", "unknown"))

        return order


@app.get("/orders")
async def list_orders():
    """List all orders."""
    with tracer.start_as_current_span("list_orders") as span:
        span.set_attribute("orders.count", len(orders_db))
        return list(orders_db.values())


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="0.0.0.0", port=8001)
