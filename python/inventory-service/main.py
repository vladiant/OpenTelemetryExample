import os
import time
import random
from fastapi import FastAPI
from pydantic import BaseModel
from opentelemetry import trace
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor
from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import OTLPSpanExporter
from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor

# Initialize OpenTelemetry
trace.set_tracer_provider(TracerProvider())
tracer_provider = trace.get_tracer_provider()

otlp_exporter = OTLPSpanExporter(
    endpoint=os.getenv("OTEL_EXPORTER_OTLP_ENDPOINT", "http://jaeger:4317"),
    insecure=True,
)

tracer_provider.add_span_processor(BatchSpanProcessor(otlp_exporter))
tracer = trace.get_tracer(__name__)

app = FastAPI(title="Inventory Service")

FastAPIInstrumentor.instrument_app(app)

# Simulated inventory
inventory = {
    1: {"product_id": 1, "name": "Laptop", "quantity": 50},
    2: {"product_id": 2, "name": "Mouse", "quantity": 200},
    3: {"product_id": 3, "name": "Keyboard", "quantity": 150},
}


class InventoryCheck(BaseModel):
    product_id: int
    quantity: int


class InventoryReserve(BaseModel):
    product_id: int
    quantity: int


@app.get("/")
async def root():
    return {"service": "inventory-service", "status": "healthy"}


@app.post("/inventory/check")
async def check_inventory(request: InventoryCheck):
    """Check if inventory is available - receives propagated trace context"""
    with tracer.start_as_current_span("inventory-service.check") as span:
        span.set_attribute("product.id", request.product_id)
        span.set_attribute("requested.quantity", request.quantity)

        # Simulate inventory check latency
        check_time = random.uniform(0.01, 0.03)
        span.add_event("Checking inventory availability")
        time.sleep(check_time)

        available_quantity = inventory.get(request.product_id, {}).get("quantity", 0)
        span.set_attribute("available.quantity", available_quantity)

        is_available = available_quantity >= request.quantity
        span.set_attribute("inventory.available", is_available)

        if is_available:
            span.add_event("Inventory available")
        else:
            span.add_event("Insufficient inventory")

        return {
            "product_id": request.product_id,
            "requested_quantity": request.quantity,
            "available_quantity": available_quantity,
            "available": is_available,
            "check_time_ms": int(check_time * 1000),
        }


@app.post("/inventory/reserve")
async def reserve_inventory(request: InventoryReserve):
    """Reserve inventory - receives propagated trace context"""
    with tracer.start_as_current_span("inventory-service.reserve") as span:
        span.set_attribute("product.id", request.product_id)
        span.set_attribute("reserve.quantity", request.quantity)

        # Simulate reservation latency
        reserve_time = random.uniform(0.02, 0.05)
        span.add_event("Reserving inventory")
        time.sleep(reserve_time)

        if request.product_id in inventory:
            current_qty = inventory[request.product_id]["quantity"]
            if current_qty >= request.quantity:
                inventory[request.product_id]["quantity"] -= request.quantity
                new_qty = inventory[request.product_id]["quantity"]

                span.add_event("Inventory reserved successfully")
                span.set_attribute("previous.quantity", current_qty)
                span.set_attribute("new.quantity", new_qty)

                return {
                    "product_id": request.product_id,
                    "reserved_quantity": request.quantity,
                    "remaining_quantity": new_qty,
                    "status": "reserved",
                    "reserve_time_ms": int(reserve_time * 1000),
                }

        span.add_event("Reservation failed")
        span.set_attribute("reservation.failed", True)

        return {
            "product_id": request.product_id,
            "status": "failed",
            "reason": "insufficient_inventory",
        }


@app.get("/inventory/{product_id}")
async def get_inventory(product_id: int):
    """Get current inventory level"""
    with tracer.start_as_current_span("inventory-service.get") as span:
        span.set_attribute("product.id", product_id)

        if product_id in inventory:
            product = inventory[product_id]
            span.set_attribute("product.quantity", product["quantity"])
            return product

        span.set_attribute("product.found", False)
        return {"error": "Product not found"}


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="0.0.0.0", port=8004)
