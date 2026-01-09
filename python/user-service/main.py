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

otlp_exporter = OTLPSpanExporter(
    endpoint=os.getenv("OTEL_EXPORTER_OTLP_ENDPOINT", "http://jaeger:4317"),
    insecure=True,
)

tracer_provider.add_span_processor(BatchSpanProcessor(otlp_exporter))
tracer = trace.get_tracer(__name__)

app = FastAPI(title="User Service")

FastAPIInstrumentor.instrument_app(app)
RequestsInstrumentor().instrument()

DATABASE_SERVICE_URL = os.getenv("DATABASE_SERVICE_URL", "http://database-service:8003")


@app.get("/")
async def root():
    return {"service": "user-service", "status": "healthy"}


@app.get("/users/{user_id}")
async def get_user(user_id: int):
    """Get user from database - trace context is propagated"""
    with tracer.start_as_current_span("user-service.get_user") as span:
        span.set_attribute("user.id", user_id)
        span.add_event("Fetching user from database")

        try:
            # Call database service - trace context propagates automatically
            response = requests.get(
                f"{DATABASE_SERVICE_URL}/query",
                params={"table": "users", "id": user_id},
            )
            response.raise_for_status()
            db_result = response.json()

            span.add_event("User fetched successfully")

            # Enrich the response
            user_data = {
                "user_id": user_id,
                "name": f"User {user_id}",
                "email": f"user{user_id}@example.com",
                "database_query_time": db_result.get("query_time_ms"),
                "status": "active",
            }

            return user_data
        except requests.exceptions.RequestException as e:
            span.record_exception(e)
            span.set_attribute("error", True)
            raise HTTPException(status_code=500, detail=str(e))


@app.post("/users")
async def create_user(name: str, email: str):
    """Create a new user"""
    with tracer.start_as_current_span("user-service.create_user") as span:
        span.set_attribute("user.name", name)
        span.set_attribute("user.email", email)

        try:
            response = requests.post(
                f"{DATABASE_SERVICE_URL}/insert",
                json={"table": "users", "data": {"name": name, "email": email}},
            )
            response.raise_for_status()

            return {"message": "User created", "data": response.json()}
        except requests.exceptions.RequestException as e:
            span.record_exception(e)
            raise HTTPException(status_code=500, detail=str(e))


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="0.0.0.0", port=8001)
