# Python Microservices with Grafana Tempo Tracing

A demonstration project showing distributed tracing with **Grafana Tempo** and **OpenTelemetry** in a Python microservices architecture.

## Architecture

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   API Gateway   │────▶│  Order Service  │────▶│Inventory Service│
│    (port 8000)  │     │   (port 8001)   │     │   (port 8002)   │
└────────┬────────┘     └────────┬────────┘     └────────┬────────┘
         │                       │                       │
         │                       │                       │
         └───────────────────────┴───────────────────────┘
                                 │
                                 ▼
                    ┌────────────────────────┐
                    │     Grafana Tempo      │
                    │   (OTLP: 4317/4318)    │
                    └────────────┬───────────┘
                                 │
                                 ▼
                    ┌────────────────────────┐
                    │        Grafana         │
                    │      (port 3000)       │
                    └────────────────────────┘
```

## Services

| Service | Port | Description |
|---------|------|-------------|
| **API Gateway** | 8000 | Entry point for all client requests |
| **Order Service** | 8001 | Handles order creation and management |
| **Inventory Service** | 8002 | Manages product inventory |
| **Grafana Tempo** | 3200 (HTTP), 4317 (gRPC), 4318 (HTTP) | Distributed tracing backend |
| **Grafana** | 3000 | Visualization and trace exploration |

## Features Demonstrated

- **Automatic Instrumentation**: FastAPI and HTTPX auto-instrumented with OpenTelemetry
- **Manual Spans**: Custom spans for business logic with attributes and events
- **Context Propagation**: Trace context automatically propagated across HTTP calls
- **Error Handling**: Proper span status and exception recording
- **Span Events**: Business events recorded within spans
- **Custom Attributes**: Service-specific metadata on spans

## Quick Start

### 1. Start the Stack

```bash
docker-compose up --build
```

### 2. Generate Traces

```bash
# Create an order (generates distributed trace across all services)
curl -X POST http://localhost:8000/orders \
  -H "Content-Type: application/json" \
  -d '{"product_id": "demo-product", "quantity": 5}'

# Get inventory
curl http://localhost:8000/inventory

# Get specific product
curl http://localhost:8000/inventory/laptop-001
```

Or run the test script:

```bash
chmod +x scripts/test_traces.sh
./scripts/test_traces.sh
```

### 3. View Traces in Grafana

1. Open [http://localhost:3000](http://localhost:3000)
2. Login with `admin` / `admin`
3. Go to **Explore** (compass icon in sidebar)
4. Select **Tempo** datasource
5. Use the **Search** tab to find traces
6. Filter by service name: `api-gateway`, `order-service`, `inventory-service`

## API Endpoints

### API Gateway (port 8000)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | Health check |
| GET | `/health` | Detailed health status |
| POST | `/orders` | Create a new order |
| GET | `/orders/{order_id}` | Get order by ID |
| GET | `/inventory` | List all inventory |
| GET | `/inventory/{product_id}` | Get product inventory |

### Order Service (port 8001)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/orders` | Create order |
| GET | `/orders` | List all orders |
| GET | `/orders/{order_id}` | Get order details |

### Inventory Service (port 8002)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/inventory` | List all products |
| GET | `/inventory/{product_id}` | Get product details |
| POST | `/inventory/{product_id}/reserve` | Reserve inventory |
| POST | `/inventory/{product_id}/release` | Release reservation |
| POST | `/inventory/{product_id}/add` | Add inventory |

## Sample Products

The inventory service comes pre-loaded with:

| Product ID | Name | Quantity | Price |
|------------|------|----------|-------|
| demo-product | Demo Product | 100 | $29.99 |
| laptop-001 | Business Laptop | 50 | $999.99 |
| phone-001 | Smartphone Pro | 200 | $699.99 |
| headphones-001 | Wireless Headphones | 75 | $149.99 |

## Trace Structure

When creating an order, the trace spans look like:

```
api-gateway: POST /orders
├── process_order_request
│   └── HTTP POST order-service/orders
│       └── order-service: POST /orders
│           ├── create_order
│           │   ├── validate_order
│           │   ├── check_inventory
│           │   │   └── HTTP GET inventory-service/inventory/{id}
│           │   │       └── inventory-service: GET /inventory/{id}
│           │   │           └── get_inventory_item
│           │   ├── reserve_inventory
│           │   │   └── HTTP POST inventory-service/inventory/{id}/reserve
│           │   │       └── inventory-service: POST /inventory/{id}/reserve
│           │   │           └── reserve_inventory
│           │   │               ├── check_availability
│           │   │               └── update_reservation
│           │   └── persist_order
```

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `SERVICE_NAME` | Name of the service for tracing | Service-specific |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Tempo OTLP endpoint | `http://tempo:4317` |
| `ORDER_SERVICE_URL` | Order service URL | `http://order-service:8001` |
| `INVENTORY_SERVICE_URL` | Inventory service URL | `http://inventory-service:8002` |

### Tempo Configuration

See `tempo/tempo.yaml` for Tempo configuration including:
- OTLP receiver (gRPC and HTTP)
- Local storage backend
- Trace retention settings

## Project Structure

```
traces-example/
├── docker-compose.yml
├── README.md
├── tempo/
│   └── tempo.yaml              # Tempo configuration
├── grafana/
│   └── provisioning/
│       └── datasources/
│           └── datasources.yaml # Grafana datasource config
├── services/
│   ├── shared/
│   │   ├── requirements.txt    # Python dependencies
│   │   └── tracing.py          # Shared tracing setup
│   ├── api-gateway/
│   │   ├── Dockerfile
│   │   └── main.py
│   ├── order-service/
│   │   ├── Dockerfile
│   │   └── main.py
│   └── inventory-service/
│       ├── Dockerfile
│       └── main.py
└── scripts/
    └── test_traces.sh          # Test script
```

## Troubleshooting

### No traces appearing in Grafana

1. Check Tempo is running: `docker-compose logs tempo`
2. Verify services can reach Tempo: Check service logs for "Tracing configured"
3. Ensure you've made some requests to generate traces

### Services not starting

1. Check Docker logs: `docker-compose logs <service-name>`
2. Ensure all ports are available (3000, 3200, 4317, 4318, 8000-8002)

### Connection refused errors

Wait a few seconds after `docker-compose up` for all services to be ready.

## Learn More

- [Grafana Tempo Documentation](https://grafana.com/docs/tempo/latest/)
- [OpenTelemetry Python](https://opentelemetry.io/docs/instrumentation/python/)
- [OpenTelemetry Specification](https://opentelemetry.io/docs/specs/)
