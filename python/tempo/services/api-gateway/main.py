"""
API Gateway Service - Entry point for all client requests.

This service demonstrates:
- Automatic FastAPI instrumentation
- Manual span creation for business logic
- Context propagation to downstream services
"""

import os
import logging
import sys

# Add shared module to path
sys.path.insert(0, "/app/shared")

from fastapi import FastAPI, HTTPException, Request
from fastapi.responses import JSONResponse
import httpx
from opentelemetry import trace
from opentelemetry.trace import Status, StatusCode

from tracing import setup_tracing, instrument_fastapi, get_tracer

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Setup tracing
SERVICE_NAME = os.getenv("SERVICE_NAME", "api-gateway")
setup_tracing(SERVICE_NAME)
tracer = get_tracer(SERVICE_NAME)

# Create FastAPI app
app = FastAPI(
    title="API Gateway",
    description="Entry point for the microservices demo",
    version="1.0.0",
)

# Instrument FastAPI
instrument_fastapi(app)

# Service URLs
ORDER_SERVICE_URL = os.getenv("ORDER_SERVICE_URL", "http://localhost:8001")
INVENTORY_SERVICE_URL = os.getenv("INVENTORY_SERVICE_URL", "http://localhost:8002")


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
        "dependencies": {
            "order_service": ORDER_SERVICE_URL,
            "inventory_service": INVENTORY_SERVICE_URL,
        },
    }


@app.post("/orders")
async def create_order(request: Request):
    """
    Create a new order - demonstrates distributed tracing across services.

    Flow: API Gateway -> Order Service -> Inventory Service
    """
    # Get request body
    try:
        body = await request.json()
    except Exception:
        body = {"product_id": "demo-product", "quantity": 1}

    # Create a custom span for business logic
    with tracer.start_as_current_span("process_order_request") as span:
        span.set_attribute("order.product_id", body.get("product_id", "unknown"))
        span.set_attribute("order.quantity", body.get("quantity", 0))

        logger.info(f"Processing order request: {body}")

        try:
            # Call Order Service
            async with httpx.AsyncClient(proxy=None) as client:
                response = await client.post(
                    f"{ORDER_SERVICE_URL}/orders",
                    json=body,
                    timeout=30.0,
                )

                if response.status_code != 200:
                    span.set_status(Status(StatusCode.ERROR, "Order creation failed"))
                    raise HTTPException(
                        status_code=response.status_code, detail=response.json()
                    )

                result = response.json()
                span.set_attribute("order.id", result.get("order_id", "unknown"))
                span.set_status(Status(StatusCode.OK))

                return result

        except httpx.RequestError as e:
            span.set_status(Status(StatusCode.ERROR, str(e)))
            span.record_exception(e)
            logger.error(f"Failed to reach order service: {e}")
            raise HTTPException(status_code=503, detail="Order service unavailable")


@app.get("/orders/{order_id}")
async def get_order(order_id: str):
    """Get order details by ID."""
    with tracer.start_as_current_span("get_order_details") as span:
        span.set_attribute("order.id", order_id)

        try:
            async with httpx.AsyncClient(proxy=None) as client:
                response = await client.get(
                    f"{ORDER_SERVICE_URL}/orders/{order_id}",
                    timeout=30.0,
                )
                return response.json()
        except httpx.RequestError as e:
            span.record_exception(e)
            raise HTTPException(status_code=503, detail="Order service unavailable")


@app.get("/inventory")
async def get_inventory():
    """Get all inventory items."""
    with tracer.start_as_current_span("fetch_inventory") as span:
        try:
            async with httpx.AsyncClient(proxy=None) as client:
                response = await client.get(
                    f"{INVENTORY_SERVICE_URL}/inventory",
                    timeout=30.0,
                )
                items = response.json()
                span.set_attribute("inventory.item_count", len(items))
                return items
        except httpx.RequestError as e:
            span.record_exception(e)
            raise HTTPException(status_code=503, detail="Inventory service unavailable")


@app.get("/inventory/{product_id}")
async def get_inventory_item(product_id: str):
    """Get inventory for a specific product."""
    with tracer.start_as_current_span("fetch_inventory_item") as span:
        span.set_attribute("product.id", product_id)

        try:
            async with httpx.AsyncClient(proxy=None) as client:
                response = await client.get(
                    f"{INVENTORY_SERVICE_URL}/inventory/{product_id}",
                    timeout=30.0,
                )
                return response.json()
        except httpx.RequestError as e:
            span.record_exception(e)
            raise HTTPException(status_code=503, detail="Inventory service unavailable")


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="0.0.0.0", port=8000)
