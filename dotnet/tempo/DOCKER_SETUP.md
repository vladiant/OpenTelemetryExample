# Docker Setup Guide - .NET Telemetry Playground

This guide explains how to build and run the .NET Telemetry Playground using Docker and Docker Compose.

## Architecture

The Docker setup provides the following services:

```
┌─────────────────────────────────────────────────────────────┐
│                      Docker Network                         │
├──────────────────────┬──────────────────┬──────────────────┤
│  ApiServiceAtFront   │   ApiService     │ Infrastructure   │
│  (Port 5000)         │   (Port 5001)    │                  │
│                      │                  │  • MongoDB       │
│                      │                  │  • Apache Pulsar │
│  ✓ HTTP              │  ✓ HTTP          │                  │
│  ✓ Health Check      │  ✓ Health Check  │                  │
│  ✓ Traces            │  ✓ Traces        │                  │
│  ✓ Metrics           │  ✓ Metrics       │                  │
└──────────────────────┴──────────────────┴──────────────────┘
```

### Services

1. **MongoDB** - Document database with replica set support
   - Port: 27017
   - Credentials: admin / password
   - Volume: mongo_data, mongo_config

2. **Apache Pulsar** - Message streaming platform
   - Broker Port: 6650
   - Admin/Web UI: 8080
   - Volumes: pulsar_data, pulsar_conf

3. **ApiService** - Backend service (ASP.NET Core)
   - Port: 5001
   - Depends on: MongoDB, Pulsar
   - Connected to MongoDB for data persistence
   - Publishes/consumes messages via Pulsar

4. **ApiServiceAtFront** - Frontend service (ASP.NET Core)
   - Port: 5000
   - Depends on: ApiService, Pulsar
   - Routes requests to backend via HTTP
   - Publishes/consumes messages via Pulsar

## Prerequisites

- Docker (version 20.10 or higher)
- Docker Compose (version 2.0 or higher)
- At least 4GB of available RAM
- 10GB of disk space for volumes

## Quick Start

### 1. Build All Services

Build Docker images for both API services:

```bash
docker-compose build
```

For rebuilding without cache:

```bash
docker-compose build --no-cache
```

### 2. Start All Services

Start all containers in the background:

```bash
docker-compose up -d
```

For viewing logs while starting:

```bash
docker-compose up
```

### 3. Verify Services Are Running

Check the status of all services:

```bash
docker-compose ps
```

Expected output shows all services with status `Up`:

```
NAME                      COMMAND                  SERVICE             STATUS
dotnet-apiservice         "dotnet DotnetTeleme…"   apiservice          Up (healthy)
dotnet-apiserviceatfront  "dotnet DotnetTeleme…"   apiserviceatfront   Up (healthy)
dotnet-mongo              "docker-entrypoint.s…"   mongo               Up (healthy)
dotnet-pulsar             "bin/pulsar standalone"  pulsar              Up (healthy)
```

### 4. Access Services

- **ApiServiceAtFront**: http://localhost:5000
- **ApiService**: http://localhost:5001
- **Pulsar Admin UI**: http://localhost:8080/admin/v2/overview
- **MongoDB**: localhost:27017 (admin / password)

## Common Commands

### View Logs

View logs from all services:

```bash
docker-compose logs -f
```

View logs from a specific service:

```bash
docker-compose logs -f apiservice
docker-compose logs -f apiserviceatfront
docker-compose logs -f mongo
docker-compose logs -f pulsar
```

Tail last 100 lines:

```bash
docker-compose logs --tail=100
```

### Stop Services

Stop all running containers:

```bash
docker-compose stop
```

Stop a specific service:

```bash
docker-compose stop apiservice
```

### Start Services

Start all containers:

```bash
docker-compose start
```

Start a specific service:

```bash
docker-compose start apiservice
```

### Restart Services

Restart all services:

```bash
docker-compose restart
```

Restart a specific service:

```bash
docker-compose restart apiservice
```

### Remove Services

Remove containers but keep volumes:

```bash
docker-compose down
```

Remove containers and volumes:

```bash
docker-compose down -v
```

### Rebuild and Restart

Rebuild and restart all services:

```bash
docker-compose down -v && docker-compose build && docker-compose up -d
```

## Health Checks

All services have health checks configured:

- **ApiService**: HTTP GET `/health` (port 8080)
- **ApiServiceAtFront**: HTTP GET `/health` (port 8080)
- **MongoDB**: mongosh admin ping
- **Pulsar**: HTTP GET `/admin/v2/brokers`

Health check intervals: 30 seconds with 5-second timeout

## Environment Variables

### Application Variables

Override environment variables by editing `docker-compose.yml` or creating a `.env` file:

```env
# OTEL Configuration (optional)
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
OTEL_EXPORTER_OTLP_HEADERS=Authorization=Bearer%20xxxx

# Application Environment
ASPNETCORE_ENVIRONMENT=Production

# MongoDB
MONGO_INITDB_ROOT_USERNAME=admin
MONGO_INITDB_ROOT_PASSWORD=password

# Memory Settings
PULSAR_MEM=-Xms512m -Xmx512m
```

### Using .env File

Create a `.env` file in the project root:

```bash
cat > .env << EOF
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
OTEL_EXPORTER_OTLP_HEADERS=
ASPNETCORE_ENVIRONMENT=Production
EOF
```

Then start with:

```bash
docker-compose --env-file .env up -d
```

## Connecting to MongoDB from Host

To connect to MongoDB from your host machine:

```bash
mongosh --authenticationDatabase admin -u admin -p password --host localhost:27017
```

Or using MongoDB Compass:

- Host: localhost
- Port: 27017
- Username: admin
- Password: password
- AuthenticationDatabase: admin

## Connecting to Pulsar from Host

Pulsar provides a web UI at http://localhost:8080/admin/v2/overview

To use Pulsar CLI:

```bash
docker exec dotnet-pulsar bin/pulsar-admin clusters list
docker exec dotnet-pulsar bin/pulsar-admin namespaces list public
docker exec dotnet-pulsar bin/pulsar-admin topics list public/default
```

## Troubleshooting

### Services Not Starting

1. Check Docker daemon is running:
   ```bash
   docker ps
   ```

2. Check logs for specific service:
   ```bash
   docker-compose logs <service-name>
   ```

3. Ensure ports are not in use:
   ```bash
   netstat -tulpn | grep -E '5000|5001|6650|8080|27017'
   ```

### MongoDB Connection Issues

1. Verify MongoDB is healthy:
   ```bash
   docker-compose ps mongo
   ```

2. Check MongoDB logs:
   ```bash
   docker-compose logs mongo
   ```

3. Test connection:
   ```bash
   mongosh --authenticationDatabase admin -u admin -p password --host localhost:27017
   ```

### Pulsar Connection Issues

1. Verify Pulsar is healthy:
   ```bash
   docker-compose ps pulsar
   ```

2. Check Pulsar logs:
   ```bash
   docker-compose logs pulsar
   ```

3. Test connection:
   ```bash
   curl http://localhost:8080/admin/v2/brokers
   ```

### API Service Errors

1. Check API service logs:
   ```bash
   docker-compose logs apiservice
   ```

2. Verify MongoDB and Pulsar are accessible:
   ```bash
   docker exec dotnet-apiservice curl http://mongo:27017
   docker exec dotnet-apiservice curl http://pulsar:6650
   ```

### Port Already in Use

If ports are already in use, modify `docker-compose.yml`:

```yaml
services:
  apiservice:
    ports:
      - "5010:8080"  # Changed from 5001:8080
  apiserviceatfront:
    ports:
      - "5009:8080"  # Changed from 5000:8080
```

### Out of Memory

If experiencing out-of-memory issues:

1. Increase Docker memory allocation
2. Reduce Pulsar memory:
   ```yaml
   environment:
     PULSAR_MEM: "-Xms256m -Xmx256m"
   ```

## Performance Optimization

### Resource Limits

Add resource limits to `docker-compose.yml`:

```yaml
services:
  apiservice:
    deploy:
      resources:
        limits:
          cpus: '1'
          memory: 512M
        reservations:
          cpus: '0.5'
          memory: 256M
```

### Volume Performance

For better performance on Mac/Windows, use named volumes instead of bind mounts:

```yaml
volumes:
  mongo_data:
    driver: local
  pulsar_data:
    driver: local
```

## OpenTelemetry Integration

To send telemetry to an external OTLP collector:

1. Uncomment the `otel-collector` service in `docker-compose.yml`
2. Set environment variables:
   ```bash
   export OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
   export OTEL_EXPORTER_OTLP_HEADERS="Authorization=Bearer your-token"
   ```

3. Restart services:
   ```bash
   docker-compose up -d
   ```

## Building for Production

### Multi-stage Build

The Dockerfiles use multi-stage builds to minimize image size:

1. **Build stage**: Compiles the .NET application
2. **Publish stage**: Creates the published application
3. **Runtime stage**: Runs the minimal runtime image

To manually build images:

```bash
docker build -f DotnetTelemetryPlayground.ApiService/Dockerfile -t dotnet-telemetry-apiservice:latest .
docker build -f DotnetTelemetryPlayground.ApiServiceAtFront/Dockerfile -t dotnet-telemetry-apiserviceatfront:latest .
```

### Registry Push

To push images to a Docker registry:

```bash
docker tag dotnet-telemetry-apiservice:latest registry.example.com/apiservice:latest
docker tag dotnet-telemetry-apiserviceatfront:latest registry.example.com/apiserviceatfront:latest

docker push registry.example.com/apiservice:latest
docker push registry.example.com/apiserviceatfront:latest
```

## Advanced Configuration

### Custom Pulsar Configuration

To customize Pulsar, create a `pulsar-conf/standalone.conf` file and mount it:

```yaml
pulsar:
  volumes:
    - ./pulsar-conf:/pulsar/conf:ro
```

### Custom MongoDB Configuration

To customize MongoDB, create a `mongo-conf/mongod.conf` file and mount it:

```yaml
mongo:
  command: mongod --config /data/configdb/mongod.conf
  volumes:
    - ./mongo-conf/mongod.conf:/data/configdb/mongod.conf:ro
```

## Data Persistence

All data is persisted in named volumes:

- `mongo_data`: MongoDB data files
- `mongo_config`: MongoDB configuration
- `pulsar_data`: Pulsar broker data
- `pulsar_conf`: Pulsar configuration

To back up volumes:

```bash
docker run --rm -v mongo_data:/data -v $(pwd):/backup alpine tar czf /backup/mongo_data.tar.gz -C / data
```

## Next Steps

1. **Test the API**: Make requests to http://localhost:5000/api/weather
2. **Monitor Pulsar**: Visit http://localhost:8080/admin/v2/overview
3. **Check MongoDB**: Connect with MongoDB Compass
4. **View Logs**: Use `docker-compose logs -f` to monitor all services
5. **Customize Configuration**: Edit environment variables and volume mounts as needed

## Support

For issues or questions:

1. Check the logs: `docker-compose logs <service>`
2. Review this guide's Troubleshooting section
3. Consult the readme.md for project architecture details
4. Check official documentation:
   - [Docker Compose](https://docs.docker.com/compose/)
   - [MongoDB Documentation](https://docs.mongodb.com/)
   - [Apache Pulsar Documentation](https://pulsar.apache.org/docs/)
   - [ASP.NET Core Documentation](https://docs.microsoft.com/aspnet/core)
