# C++ Microservices with Grafana Tempo Tracing

A demonstration project showing distributed tracing with **Grafana Tempo** and **OpenTelemetry** in a C++ microservices architecture.

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

## Technology Stack

- **Language**: C++17
- **HTTP Server**: cpp-httplib
- **JSON**: nlohmann/json
- **Tracing**: OpenTelemetry C++ SDK
- **Trace Backend**: Grafana Tempo
- **Visualization**: Grafana

## Services

| Service | Port | Description |
|---------|------|-------------|
| **API Gateway** | 8000 | Entry point for all client requests |
| **Order Service** | 8001 | Handles order creation and management |
| **Inventory Service** | 8002 | Manages product inventory |
| **Grafana Tempo** | 3200 (HTTP), 4317 (gRPC), 4318 (HTTP) | Distributed tracing backend |
| **Grafana** | 3000 | Visualization and trace exploration |

## Features Demonstrated

- **Manual Span Creation**: Custom spans for business logic with attributes and events
- **Context Propagation**: W3C Trace Context propagation across HTTP calls
- **Error Handling**: Proper span status and exception recording
- **Span Events**: Business events recorded within spans
- **Custom Attributes**: Service-specific metadata on spans

## Quick Start

### 1. Start the Stack

```bash
docker-compose up --build
```

> **Note**: The first build will take several minutes as it compiles OpenTelemetry C++ SDK and dependencies.

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

## Project Structure

```
traces-example-cpp/
├── docker-compose.yml
├── README.md
├── tempo/
│   └── tempo.yaml              # Tempo configuration
├── grafana/
│   └── provisioning/
│       └── datasources/
│           └── datasources.yaml # Grafana datasource config
├── services/
│   ├── common/
│   │   ├── tracing.hpp         # Shared tracing utilities
│   │   └── http_client.hpp     # HTTP client with tracing
│   ├── api-gateway/
│   │   ├── Dockerfile
│   │   ├── CMakeLists.txt
│   │   └── main.cpp
│   ├── order-service/
│   │   ├── Dockerfile
│   │   ├── CMakeLists.txt
│   │   └── main.cpp
│   └── inventory-service/
│       ├── Dockerfile
│       ├── CMakeLists.txt
│       └── main.cpp
└── scripts/
    └── test_traces.sh          # Test script
```

## Dependencies

The project uses the following C++ libraries (installed during Docker build):

- **opentelemetry-cpp v1.14.2**: OpenTelemetry SDK for C++
- **cpp-httplib v0.14.3**: Header-only HTTP server/client library
- **nlohmann/json v3.11.3**: JSON library for modern C++
- **gRPC**: For OTLP gRPC exporter

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `SERVICE_NAME` | Name of the service for tracing | Service-specific |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Tempo OTLP endpoint | `tempo:4317` |
| `ORDER_SERVICE_HOST` | Order service hostname | `order-service` |
| `ORDER_SERVICE_PORT` | Order service port | `8001` |
| `INVENTORY_SERVICE_HOST` | Inventory service hostname | `inventory-service` |
| `INVENTORY_SERVICE_PORT` | Inventory service port | `8002` |

## Building Locally (without Docker)

If you want to build locally, you need to install the dependencies first:

```bash
# Install system dependencies (Ubuntu/Debian)
sudo apt-get install -y \
    build-essential cmake git pkg-config \
    libssl-dev libcurl4-openssl-dev \
    libprotobuf-dev protobuf-compiler \
    libgrpc++-dev protobuf-compiler-grpc

# Clone and build nlohmann/json
git clone --depth 1 --branch v3.11.3 https://github.com/nlohmann/json.git
cd json && mkdir build && cd build
cmake .. -DJSON_BuildTests=OFF && make && sudo make install

# Clone and build cpp-httplib
git clone --depth 1 --branch v0.14.3 https://github.com/yhirose/cpp-httplib.git
cd cpp-httplib && mkdir build && cd build
cmake .. -DHTTPLIB_COMPILE=ON && make && sudo make install

# Clone and build opentelemetry-cpp
git clone --depth 1 --branch v1.14.2 https://github.com/open-telemetry/opentelemetry-cpp.git
cd opentelemetry-cpp && mkdir build && cd build
cmake .. -DCMAKE_BUILD_TYPE=Release -DBUILD_TESTING=OFF \
    -DWITH_OTLP_GRPC=ON -DWITH_OTLP_HTTP=OFF -DWITH_ABSEIL=OFF
make && sudo make install

# Build a service
cd services/api-gateway
mkdir build && cd build
cmake .. && make
```

## Troubleshooting

### Build takes too long

The first Docker build compiles OpenTelemetry C++ SDK which takes several minutes. Subsequent builds will be faster due to Docker layer caching.

### No traces appearing in Grafana

1. Check Tempo is running: `docker-compose logs tempo`
2. Verify services can reach Tempo: Check service logs for "Tracing configured"
3. Ensure you've made some requests to generate traces

### Services not starting

1. Check Docker logs: `docker-compose logs <service-name>`
2. Ensure all ports are available (3000, 3200, 4317, 4318, 8000-8002)

## Learn More

- [Grafana Tempo Documentation](https://grafana.com/docs/tempo/latest/)
- [OpenTelemetry C++](https://opentelemetry.io/docs/instrumentation/cpp/)
- [cpp-httplib](https://github.com/yhirose/cpp-httplib)
- [nlohmann/json](https://github.com/nlohmann/json)
