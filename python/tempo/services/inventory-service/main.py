"""
Inventory Service - Manages product inventory.

This service demonstrates:
- Leaf service in the trace chain
- Database-like operations with spans
- Error handling and span status
- Custom span events and attributes
"""

import os
import logging
import sys
import asyncio
from typing import Dict, Any

# Add shared module to path
sys.path.insert(0, "/app/shared")

from fastapi import FastAPI, HTTPException
from opentelemetry import trace
from opentelemetry.trace import Status, StatusCode

from tracing import setup_tracing, instrument_fastapi, get_tracer

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Setup tracing
SERVICE_NAME = os.getenv("SERVICE_NAME", "inventory-service")
setup_tracing(SERVICE_NAME)
tracer = get_tracer(SERVICE_NAME)

# Create FastAPI app
app = FastAPI(
    title="Inventory Service",
    description="Manages product inventory",
    version="1.0.0",
)

# Instrument FastAPI
instrument_fastapi(app)

# In-memory inventory storage (simulating a database)
inventory_db: Dict[str, Dict[str, Any]] = {
    "demo-product": {
        "product_id": "demo-product",
        "name": "Demo Product",
        "quantity": 100,
        "price": 29.99,
        "reserved": 0,
    },
    "laptop-001": {
        "product_id": "laptop-001",
        "name": "Business Laptop",
        "quantity": 50,
        "price": 999.99,
        "reserved": 0,
    },
    "phone-001": {
        "product_id": "phone-001",
        "name": "Smartphone Pro",
        "quantity": 200,
        "price": 699.99,
        "reserved": 0,
    },
    "headphones-001": {
        "product_id": "headphones-001",
        "name": "Wireless Headphones",
        "quantity": 75,
        "price": 149.99,
        "reserved": 0,
    },
}

# Track reservations
reservations: Dict[str, Dict[str, Any]] = {}


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
        "products_count": len(inventory_db),
        "total_items": sum(item["quantity"] for item in inventory_db.values()),
    }


@app.get("/inventory")
async def list_inventory():
    """List all inventory items."""
    with tracer.start_as_current_span("list_inventory") as span:
        span.set_attribute("db.system", "in-memory")
        span.set_attribute("db.operation", "select")
        span.set_attribute("db.table", "inventory")

        # Simulate database query
        await asyncio.sleep(0.02)

        items = list(inventory_db.values())
        span.set_attribute("result.count", len(items))

        span.add_event(
            "Inventory query completed",
            {
                "items_returned": len(items),
            },
        )

        return items


@app.get("/inventory/{product_id}")
async def get_inventory_item(product_id: str):
    """Get inventory for a specific product."""
    with tracer.start_as_current_span("get_inventory_item") as span:
        span.set_attribute("db.system", "in-memory")
        span.set_attribute("db.operation", "select")
        span.set_attribute("db.table", "inventory")
        span.set_attribute("product.id", product_id)

        # Simulate database lookup
        await asyncio.sleep(0.01)

        if product_id not in inventory_db:
            span.set_status(Status(StatusCode.ERROR, "Product not found"))
            span.add_event(
                "Product lookup failed",
                {
                    "product_id": product_id,
                    "reason": "not_found",
                },
            )
            raise HTTPException(status_code=404, detail="Product not found")

        item = inventory_db[product_id]
        available = item["quantity"] - item["reserved"]

        span.set_attribute("inventory.quantity", item["quantity"])
        span.set_attribute("inventory.reserved", item["reserved"])
        span.set_attribute("inventory.available", available)

        span.add_event(
            "Product found",
            {
                "product_id": product_id,
                "available": available,
            },
        )

        return {
            **item,
            "available": available,
        }


@app.post("/inventory/{product_id}/reserve")
async def reserve_inventory(product_id: str, reservation: dict):
    """Reserve inventory for an order."""
    quantity = reservation.get("quantity", 0)
    order_id = reservation.get("order_id", "unknown")

    with tracer.start_as_current_span("reserve_inventory") as span:
        span.set_attribute("db.system", "in-memory")
        span.set_attribute("db.operation", "update")
        span.set_attribute("db.table", "inventory")
        span.set_attribute("product.id", product_id)
        span.set_attribute("reservation.quantity", quantity)
        span.set_attribute("reservation.order_id", order_id)

        logger.info(f"Reserving {quantity} units of {product_id} for order {order_id}")

        # Check product exists
        if product_id not in inventory_db:
            span.set_status(Status(StatusCode.ERROR, "Product not found"))
            raise HTTPException(status_code=404, detail="Product not found")

        item = inventory_db[product_id]
        available = item["quantity"] - item["reserved"]

        # Check availability
        with tracer.start_as_current_span("check_availability") as check_span:
            check_span.set_attribute("inventory.available", available)
            check_span.set_attribute("inventory.requested", quantity)

            if available < quantity:
                check_span.set_status(
                    Status(StatusCode.ERROR, "Insufficient inventory")
                )
                check_span.add_event(
                    "Reservation failed",
                    {
                        "reason": "insufficient_inventory",
                        "available": available,
                        "requested": quantity,
                    },
                )
                raise HTTPException(
                    status_code=400,
                    detail=f"Insufficient inventory. Available: {available}, Requested: {quantity}",
                )

            check_span.add_event("Availability confirmed")

        # Perform reservation
        with tracer.start_as_current_span("update_reservation") as update_span:
            update_span.set_attribute("db.operation", "update")

            # Simulate database transaction
            await asyncio.sleep(0.03)

            # Update inventory
            inventory_db[product_id]["reserved"] += quantity

            # Store reservation record
            reservations[order_id] = {
                "order_id": order_id,
                "product_id": product_id,
                "quantity": quantity,
                "status": "reserved",
            }

            update_span.add_event(
                "Reservation committed",
                {
                    "new_reserved": inventory_db[product_id]["reserved"],
                },
            )

        span.set_status(Status(StatusCode.OK))
        span.add_event("Reservation completed successfully")

        logger.info(f"Reserved {quantity} units of {product_id} for order {order_id}")

        return {
            "status": "reserved",
            "product_id": product_id,
            "quantity": quantity,
            "order_id": order_id,
            "remaining_available": item["quantity"]
            - inventory_db[product_id]["reserved"],
        }


@app.post("/inventory/{product_id}/release")
async def release_inventory(product_id: str, release_data: dict):
    """Release reserved inventory (e.g., order cancelled)."""
    order_id = release_data.get("order_id")

    with tracer.start_as_current_span("release_inventory") as span:
        span.set_attribute("product.id", product_id)
        span.set_attribute("order.id", order_id)

        if order_id not in reservations:
            span.set_status(Status(StatusCode.ERROR, "Reservation not found"))
            raise HTTPException(status_code=404, detail="Reservation not found")

        reservation = reservations[order_id]
        quantity = reservation["quantity"]

        # Simulate database update
        await asyncio.sleep(0.02)

        # Release the reservation
        inventory_db[product_id]["reserved"] -= quantity
        del reservations[order_id]

        span.set_attribute("released.quantity", quantity)
        span.add_event(
            "Inventory released",
            {
                "quantity": quantity,
                "order_id": order_id,
            },
        )

        return {
            "status": "released",
            "product_id": product_id,
            "quantity": quantity,
            "order_id": order_id,
        }


@app.post("/inventory/{product_id}/add")
async def add_inventory(product_id: str, data: dict):
    """Add inventory for a product."""
    quantity = data.get("quantity", 0)

    with tracer.start_as_current_span("add_inventory") as span:
        span.set_attribute("product.id", product_id)
        span.set_attribute("quantity.added", quantity)

        if product_id not in inventory_db:
            span.set_status(Status(StatusCode.ERROR, "Product not found"))
            raise HTTPException(status_code=404, detail="Product not found")

        # Simulate database update
        await asyncio.sleep(0.02)

        inventory_db[product_id]["quantity"] += quantity
        new_quantity = inventory_db[product_id]["quantity"]

        span.set_attribute("quantity.new_total", new_quantity)
        span.add_event(
            "Inventory added",
            {
                "added": quantity,
                "new_total": new_quantity,
            },
        )

        return {
            "product_id": product_id,
            "quantity_added": quantity,
            "new_total": new_quantity,
        }


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="0.0.0.0", port=8002)
