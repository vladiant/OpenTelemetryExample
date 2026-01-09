import os
import time
import random
from fastapi import FastAPI
from pydantic import BaseModel
from typing import Dict, Any
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

app = FastAPI(title="Database Service")

FastAPIInstrumentor.instrument_app(app)


class InsertRequest(BaseModel):
    table: str
    data: Dict[str, Any]


@app.get("/")
async def root():
    return {"service": "database-service", "status": "healthy"}


@app.get("/query")
async def query_database(table: str, id: int):
    """Simulate database query - receives propagated trace context"""
    with tracer.start_as_current_span("database-service.query") as span:
        span.set_attribute("db.system", "postgresql")
        span.set_attribute("db.name", "microservices_db")
        span.set_attribute("db.table", table)
        span.set_attribute("db.operation", "SELECT")
        span.set_attribute("db.record_id", id)

        # Simulate database query latency
        query_time = random.uniform(0.01, 0.05)
        span.add_event("Executing database query")
        time.sleep(query_time)
        span.add_event("Query completed")

        span.set_attribute("db.query_time_ms", int(query_time * 1000))

        return {
            "table": table,
            "record_id": id,
            "query_time_ms": int(query_time * 1000),
            "result": "success",
        }


@app.post("/insert")
async def insert_database(request: InsertRequest):
    """Simulate database insert - receives propagated trace context"""
    with tracer.start_as_current_span("database-service.insert") as span:
        span.set_attribute("db.system", "postgresql")
        span.set_attribute("db.name", "microservices_db")
        span.set_attribute("db.table", request.table)
        span.set_attribute("db.operation", "INSERT")

        # Simulate database insert latency
        insert_time = random.uniform(0.02, 0.08)
        span.add_event("Executing database insert")
        time.sleep(insert_time)
        span.add_event("Insert completed")

        span.set_attribute("db.insert_time_ms", int(insert_time * 1000))

        return {
            "table": request.table,
            "insert_time_ms": int(insert_time * 1000),
            "result": "success",
            "data": request.data,
        }


@app.put("/update")
async def update_database(table: str, id: int, data: Dict[str, Any]):
    """Simulate database update"""
    with tracer.start_as_current_span("database-service.update") as span:
        span.set_attribute("db.system", "postgresql")
        span.set_attribute("db.name", "microservices_db")
        span.set_attribute("db.table", table)
        span.set_attribute("db.operation", "UPDATE")
        span.set_attribute("db.record_id", id)

        update_time = random.uniform(0.02, 0.06)
        span.add_event("Executing database update")
        time.sleep(update_time)
        span.add_event("Update completed")

        return {
            "table": table,
            "record_id": id,
            "update_time_ms": int(update_time * 1000),
            "result": "success",
        }


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="0.0.0.0", port=8003)
