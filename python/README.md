# OpenTelemetry Trace Context Propagation Demo

This project demonstrates OpenTelemetry trace context propagation across multiple Python microservices using FastAPI, with Jaeger for trace visualization.

## Architecture

The demo consists of 5 microservices:

1. **API Gateway** (port 8000) - Entry point for all requests
2. **User Service** (port 8001) - Handles user operations
3. **Order Service** (port 8002) - Manages order creation and retrieval
4. **Database Service** (port 8003) - Simulates database operations
5. **Inventory Service** (port 8004) - Manages product inventory

All services are instrumented with OpenTelemetry and send traces to Jaeger.

## Key Features

- **Automatic trace context propagation** across HTTP requests
- **Distributed tracing** showing the complete request flow
- **Custom spans** for business logic
- **Span attributes** for contextual information
- **Span events** for significant operations
- **Error tracking** with exception recording

## Project Structure

```
.
├── docker-compose.yml
├── README.md
├── test_traces.sh
└── services/
    ├── api-gateway/
    │   ├── Dockerfile
    │   ├── requirements.txt
    │   └── main.py
    ├── user-service/
    │   ├── Dockerfile
    │   ├── requirements.txt
    │   └── main.py
    ├── order-service/
    │   ├── Dockerfile
    │   ├── requirements.txt
    │   └── main.py
    ├── database-service/
    │   ├── Dockerfile
    │   ├── requirements.txt
    │   └── main.py
    └── inventory-service/
        ├── Dockerfile
        ├── requirements.txt
        └── main.py
```

## Setup Instructions

### Prerequisites

- Docker and Docker Compose installed
- Ports 8000-8004 and 16686 available

### 1. Create Directory Structure

```bash
mkdir -p otel-demo/services/{api-gateway,user-service,order-service,database-service,inventory-service}
cd otel-demo
```

### 2. Copy Files

Place the provided files in the following locations:

- `docker-compose.yml` in the root directory
- `requirements.txt` in the root directory (copy to each service directory)
- Service `main.py` files in their respective `services/*/` directories
- `Dockerfile` in each service directory

For each service directory, copy both `requirements.txt` and `Dockerfile`:

```bash
for service in api-gateway user-service order-service database-service inventory-service; do
  cp requirements.txt services/$service/
  cp Dockerfile services/$service/
done
```

### 3. Start the Services

```bash
docker-compose up --build
```

Wait for all services to start. You should see log messages indicating services are running.

### 4. Access Jaeger UI

Open your browser and navigate to:
```
http://localhost:16686
```

## Testing the Demo

### Test 1: Simple User Lookup

This demonstrates trace propagation through:
API Gateway → User Service → Database Service

```bash
curl http://localhost:8000/users/123
```

**Expected trace:**
- api-gateway.get_user
  - user-service.get_user
    - database-service.query

### Test 2: Complex Order Creation

This demonstrates complex trace propagation through:
API Gateway → User Service → Database Service
           → Order Service → Inventory Service
                          → Database Service

```bash
curl -X POST "http://localhost:8000/orders?user_id=123&product_id=1&quantity=2"
```

**Expected trace:**
- api-gateway.create_order
  - user-service.get_user
    - database-service.query
  - order-service.create_order
    - check_inventory
      - inventory-service.check
    - create_order_record
      - database-service.insert
    - reserve_inventory
      - inventory-service.reserve

### Test 3: Order Retrieval

```bash
curl http://localhost:8000/orders/456
```

### Test 4: Direct Service Access

You can also test individual services:

```bash
# User Service
curl http://localhost:8001/users/789

# Order Service
curl http://localhost:8002/orders/123

# Inventory Check
curl -X POST http://localhost:8004/inventory/check \
  -H "Content-Type: application/json" \
  -d '{"product_id": 1, "quantity": 5}'

# Database Query
curl "http://localhost:8003/query?table=users&id=123"
```

## Viewing Traces in Jaeger

1. Open Jaeger UI at `http://localhost:16686`
2. Select a service from the "Service" dropdown (e.g., `api-gateway`)
3. Click "Find Traces"
4. Click on a trace to see the complete distributed trace
5. Observe:
   - **Trace timeline**: Shows how long each operation took
   - **Span hierarchy**: Shows the call chain between services
   - **Span details**: Shows attributes, events, and any errors
   - **Service dependencies**: Visual representation of service interactions

## Key OpenTelemetry Concepts Demonstrated

### 1. Automatic Instrumentation

```python
from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor
from opentelemetry.instrumentation.requests import RequestsInstrumentor

FastAPIInstrumentor.instrument_app(app)
RequestsInstrumentor().instrument()
```

These automatically create spans for:
- All FastAPI endpoints
- All outgoing HTTP requests made with `requests` library

### 2. Manual Span Creation

```python
with tracer.start_as_current_span("operation_name") as span:
    span.set_attribute("key", "value")
    span.add_event("Something happened")
    # Your code here
```

### 3. Trace Context Propagation

When a service makes an HTTP request to another service, OpenTelemetry automatically:
- Extracts the trace context from incoming requests
- Injects the trace context into outgoing requests
- Links parent and child spans

This is done via W3C Trace Context headers:
- `traceparent`: Contains trace ID, span ID, and flags
- `tracestate`: Contains vendor-specific data

### 4. Span Attributes

Attributes provide context about operations:

```python
span.set_attribute("user.id", user_id)
span.set_attribute("db.system", "postgresql")
span.set_attribute("http.status_code", 200)
```

### 5. Span Events

Events mark significant moments within a span:

```python
span.add_event("User lookup started")
span.add_event("Database query completed")
```

### 6. Error Recording

Exceptions are automatically recorded:

```python
try:
    # operation
except Exception as e:
    span.record_exception(e)
    span.set_attribute("error", True)
    raise
```

## Understanding the Trace

When you create an order, you'll see a trace like this:

```
api-gateway.create_order (100ms)
├─ user-service.get_user (30ms)
│  └─ database-service.query (20ms)
├─ order-service.create_order (60ms)
   ├─ check_inventory (15ms)
   │  └─ inventory-service.check (10ms)
   ├─ create_order_record (25ms)
   │  └─ database-service.insert (20ms)
   └─ reserve_inventory (20ms)
      └─ inventory-service.reserve (15ms)
```

Each span shows:
- Service name and operation
- Duration
- Parent-child relationships
- Custom attributes
- Events that occurred
- Any errors

## Cleanup

Stop and remove all containers:

```bash
docker-compose down
```

Remove volumes (if needed):

```bash
docker-compose down -v
```

## Troubleshooting

### Services won't start
- Check if ports are already in use
- Ensure Docker has enough resources allocated

### Traces not appearing in Jaeger
- Wait a few seconds for traces to be exported
- Check service logs: `docker-compose logs [service-name]`
- Verify OTLP endpoint is correct in environment variables

### Connection errors between services
- Ensure all services are in the same Docker network
- Check service names in environment variables match container names

## Advanced Exercises

1. **Add a new service**: Create a notification service that gets called after order creation
2. **Add metrics**: Instrument services with OpenTelemetry metrics
3. **Add logging**: Correlate logs with traces using trace IDs
4. **Sampling**: Implement trace sampling to reduce data volume
5. **Custom propagators**: Experiment with different context propagation formats
6. **Baggage**: Use OpenTelemetry Baggage to propagate custom data across services

## Resources

- [OpenTelemetry Python Documentation](https://opentelemetry.io/docs/instrumentation/python/)
- [OpenTelemetry Specification](https://opentelemetry.io/docs/specs/otel/)
- [Jaeger Documentation](https://www.jaegertracing.io/docs/)
- [W3C Trace Context](https://www.w3.org/TR/trace-context/)

## License

This demo is provided for educational purposes.