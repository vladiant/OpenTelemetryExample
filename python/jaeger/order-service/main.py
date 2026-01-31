import os
import requests
import time
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from opentelemetry import trace
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor
from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import OTLPSpanExporter
from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor
from opentelemetry.instrumentation.requests import RequestsInstrumentor

# Initialize OpenTelemetry
trace.set_tracer_provider(TracerProvider())
tracer_provider = trace.get_tracer_provider()

otlp_exporter = OTLPSpanExporter(
    endpoint=os.getenv("OTEL_EXPORTER_OTLP_ENDPOINT", "http://jaeger:4317"),
    insecure=True,
)

tracer_provider.add_span_processor(BatchSpanProcessor(otlp_exporter))
tracer = trace.get_tracer(__name__)

app = FastAPI(title="Order Service")

FastAPIInstrumentor.instrument_app(app)
RequestsInstrumentor().instrument()

DATABASE_SERVICE_URL = os.getenv("DATABASE_SERVICE_URL", "http://database-service:8003")
INVENTORY_SERVICE_URL = os.getenv(
    "INVENTORY_SERVICE_URL", "http://inventory-service:8004"
)


class OrderRequest(BaseModel):
    user_id: int
    product_id: int
    quantity: int


@app.get("/")
async def root():
    return {"service": "order-service", "status": "healthy"}


@app.post("/orders")
async def create_order(order: OrderRequest):
    """Create order with inventory check - demonstrates multi-service trace propagation"""
    with tracer.start_as_current_span("order-service.create_order") as span:
        span.set_attribute("user.id", order.user_id)
        span.set_attribute("product.id", order.product_id)
        span.set_attribute("order.quantity", order.quantity)

        try:
            # Step 1: Check inventory availability
            with tracer.start_as_current_span("check_inventory") as inv_span:
                inv_span.add_event("Checking inventory availability")
                inv_response = requests.post(
                    f"{INVENTORY_SERVICE_URL}/inventory/check",
                    json={"product_id": order.product_id, "quantity": order.quantity},
                )
                inv_response.raise_for_status()
                inventory_data = inv_response.json()

                if not inventory_data.get("available"):
                    inv_span.set_attribute("inventory.available", False)
                    raise HTTPException(
                        status_code=400, detail="Insufficient inventory"
                    )

                inv_span.set_attribute("inventory.available", True)

            # Step 2: Create order in database
            with tracer.start_as_current_span("create_order_record") as db_span:
                db_span.add_event("Creating order record in database")
                order_id = int(time.time() * 1000) % 1000000

                db_response = requests.post(
                    f"{DATABASE_SERVICE_URL}/insert",
                    json={
                        "table": "orders",
                        "data": {
                            "order_id": order_id,
                            "user_id": order.user_id,
                            "product_id": order.product_id,
                            "quantity": order.quantity,
                            "status": "pending",
                        },
                    },
                )
                db_response.raise_for_status()

            # Step 3: Reserve inventory
            with tracer.start_as_current_span("reserve_inventory") as reserve_span:
                reserve_span.add_event("Reserving inventory")
                reserve_response = requests.post(
                    f"{INVENTORY_SERVICE_URL}/inventory/reserve",
                    json={"product_id": order.product_id, "quantity": order.quantity},
                )
                reserve_response.raise_for_status()

            span.add_event("Order created successfully")
            span.set_attribute("order.id", order_id)

            return {
                "order_id": order_id,
                "status": "created",
                "user_id": order.user_id,
                "product_id": order.product_id,
                "quantity": order.quantity,
            }

        except requests.exceptions.RequestException as e:
            span.record_exception(e)
            span.set_attribute("error", True)
            raise HTTPException(status_code=500, detail=str(e))


@app.get("/orders/{order_id}")
async def get_order(order_id: int):
    """Get order details from database"""
    with tracer.start_as_current_span("order-service.get_order") as span:
        span.set_attribute("order.id", order_id)

        try:
            response = requests.get(
                f"{DATABASE_SERVICE_URL}/query",
                params={"table": "orders", "id": order_id},
            )
            response.raise_for_status()

            return {
                "order_id": order_id,
                "status": "completed",
                "database_result": response.json(),
            }
        except requests.exceptions.RequestException as e:
            span.record_exception(e)
            raise HTTPException(status_code=500, detail=str(e))


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="0.0.0.0", port=8002)
