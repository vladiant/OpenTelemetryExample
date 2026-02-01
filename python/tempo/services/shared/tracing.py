"""Shared OpenTelemetry tracing configuration for all microservices."""

import os
import logging

from opentelemetry import trace
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor
from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import OTLPSpanExporter
from opentelemetry.sdk.resources import Resource, SERVICE_NAME
from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor
from opentelemetry.instrumentation.httpx import HTTPXClientInstrumentor
from opentelemetry.instrumentation.logging import LoggingInstrumentor
from opentelemetry.trace.propagation.tracecontext import TraceContextTextMapPropagator

logger = logging.getLogger(__name__)


def setup_tracing(service_name: str) -> trace.Tracer:
    """
    Configure OpenTelemetry tracing with OTLP exporter to Tempo.

    Args:
        service_name: Name of the service for trace identification

    Returns:
        Configured tracer instance
    """
    # Get OTLP endpoint from environment
    otlp_endpoint = os.getenv("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317")

    # Create resource with service name
    resource = Resource.create(
        {
            SERVICE_NAME: service_name,
            "service.version": "1.0.0",
            "deployment.environment": os.getenv("ENVIRONMENT", "development"),
        }
    )

    # Create tracer provider
    provider = TracerProvider(resource=resource)

    # Configure OTLP exporter
    otlp_exporter = OTLPSpanExporter(
        endpoint=otlp_endpoint,
        insecure=True,
    )

    # Add batch processor for efficient span export
    processor = BatchSpanProcessor(otlp_exporter)
    provider.add_span_processor(processor)

    # Set global tracer provider
    trace.set_tracer_provider(provider)

    # Instrument logging to include trace context
    LoggingInstrumentor().instrument(set_logging_format=True)

    # Instrument HTTPX client for outgoing requests
    HTTPXClientInstrumentor().instrument()

    logger.info(f"Tracing configured for {service_name} -> {otlp_endpoint}")

    return trace.get_tracer(service_name)


def instrument_fastapi(app):
    """Instrument FastAPI application for automatic tracing."""
    FastAPIInstrumentor.instrument_app(app)
    return app


def get_tracer(name: str) -> trace.Tracer:
    """Get a tracer instance for manual instrumentation."""
    return trace.get_tracer(name)


# Propagator for context propagation across services
propagator = TraceContextTextMapPropagator()
