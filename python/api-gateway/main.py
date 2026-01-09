import os
import requests
from fastapi import FastAPI, HTTPException
from opentelemetry import trace
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor
from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import OTLPSpanExporter
from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor
from opentelemetry.instrumentation.requests import RequestsInstrumentor

# Initialize OpenTelemetry
trace.set_tracer_provider(TracerProvider())
tracer_provider = trace.get_tracer_provider()

# Configure OTLP exporter
otlp_exporter = OTLPSpanExporter(
    endpoint=os.getenv("OTEL_EXPORTER_OTLP_ENDPOINT", "http://jaeger:4317"),
    insecure=True,
)

# Add span processor
tracer_provider.add_span_processor(BatchSpanProcessor(otlp_exporter))

# Get tracer
tracer = trace.get_tracer(__name__)

# Initialize FastAPI
app = FastAPI(title="API Gateway")

# Auto-instrument FastAPI and requests
FastAPIInstrumentor.instrument_app(app)
RequestsInstrumentor().instrument()

USER_SERVICE_URL = os.getenv("USER_SERVICE_URL", "http://user-service:8001")
ORDER_SERVICE_URL = os.getenv("ORDER_SERVICE_URL", "http://order-service:8002")


@app.get("/")
async def root():
    return {"service": "api-gateway", "status": "healthy"}


@app.get("/users/{user_id}")
async def get_user(user_id: int):
    """Get user information - demonstrates trace propagation"""
    with tracer.start_as_current_span("api-gateway.get_user") as span:
        span.set_attribute("user.id", user_id)

        try:
            response = requests.get(f"{USER_SERVICE_URL}/users/{user_id}")
            response.raise_for_status()
            return response.json()
        except requests.exceptions.RequestException as e:
            span.record_exception(e)
            raise HTTPException(status_code=500, detail=str(e))


@app.post("/orders")
async def create_order(user_id: int, product_id: int, quantity: int):
    """Create an order - demonstrates complex trace propagation"""
    with tracer.start_as_current_span("api-gateway.create_order") as span:
        span.set_attribute("user.id", user_id)
        span.set_attribute("product.id", product_id)
        span.set_attribute("order.quantity", quantity)

        try:
            # First verify user exists
            user_response = requests.get(f"{USER_SERVICE_URL}/users/{user_id}")
            user_response.raise_for_status()

            # Then create the order
            order_response = requests.post(
                f"{ORDER_SERVICE_URL}/orders",
                json={
                    "user_id": user_id,
                    "product_id": product_id,
                    "quantity": quantity,
                },
            )
            order_response.raise_for_status()

            return order_response.json()
        except requests.exceptions.RequestException as e:
            span.record_exception(e)
            raise HTTPException(status_code=500, detail=str(e))


@app.get("/orders/{order_id}")
async def get_order(order_id: int):
    """Get order details - demonstrates trace propagation"""
    with tracer.start_as_current_span("api-gateway.get_order") as span:
        span.set_attribute("order.id", order_id)

        try:
            response = requests.get(f"{ORDER_SERVICE_URL}/orders/{order_id}")
            response.raise_for_status()
            return response.json()
        except requests.exceptions.RequestException as e:
            span.record_exception(e)
            raise HTTPException(status_code=500, detail=str(e))


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="0.0.0.0", port=8000)
