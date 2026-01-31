Create a docker-compose project demonstrating OpenTelemetry with a focus on trace context propagation across various Python based microservices

I'll create a comprehensive Docker Compose project demonstrating OpenTelemetry with trace context propagation across Python microservices. This will include multiple services communicating with each other, all instrumented with OpenTelemetry.Now let me create the service files. I'll start with a shared requirements file and base configuration:Now let me create the Dockerfile that will be shared across all services:Now let me create a comprehensive README with setup instructions and testing examples:Finally, let me create a helpful test script:Perfect! I've created a comprehensive OpenTelemetry demonstration project with trace context propagation across Python microservices. Here's what I've built:

## 🎯 Project Overview

**5 Microservices Architecture:**
1. **API Gateway** - Entry point routing requests
2. **User Service** - User management
3. **Order Service** - Order processing with inventory checks
4. **Database Service** - Simulated database operations
5. **Inventory Service** - Product inventory management

**Key Features:**
- ✅ Automatic trace context propagation using W3C Trace Context
- ✅ Custom spans for business logic
- ✅ Span attributes, events, and error tracking
- ✅ Jaeger for trace visualization
- ✅ Complex multi-service workflows

## 📁 Files Created

1. **docker-compose.yml** - Orchestrates all services and Jaeger
2. **requirements.txt** - Python dependencies
3. **Service implementations** (5 main.py files)
4. **Dockerfile** - Shared container configuration
5. **README.md** - Complete setup and usage guide
6. **test_traces.sh** - Automated testing script

## 🚀 Quick Start

```bash
# Then run:
docker-compose up --build

# Test the system
chmod +x test_traces.sh
./test_traces.sh

# View traces at:
http://localhost:16686
```

## 🔍 What Makes This Demo Special

1. **Real trace propagation** - Watch traces flow across 5 services in complex workflows
2. **Multiple trace patterns** - Simple calls and complex multi-service transactions
3. **Production-ready patterns** - Uses proper instrumentation with FastAPI and requests
4. **Educational value** - Clear code comments and comprehensive README
5. **Visual feedback** - Beautiful trace visualization in Jaeger showing the complete request journey

The order creation flow is particularly impressive - it touches 4 different services with proper trace context propagation, showing exactly how distributed tracing works in production microservices!
