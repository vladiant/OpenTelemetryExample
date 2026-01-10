# Conan Build System Guide

This document explains the Conan-based build system for the OpenTelemetry C++ microservices project.

## Overview

The project uses **Conan 1.x** as the C++ package manager to handle dependencies. This provides several benefits:

### Benefits of Using Conan

1. **Faster Builds**: Binary packages are downloaded instead of compiling everything from source
2. **Reproducible Builds**: Exact dependency versions are specified and locked
3. **Cross-Platform**: Works on Linux, macOS, and Windows
4. **Dependency Management**: Automatically handles transitive dependencies
5. **Version Control**: Easy to upgrade or downgrade package versions

## Architecture

### Build Flow

```
conanfile.txt → Conan Downloads Binaries → CMake Configuration → Compilation → Linking
```

### File Structure

```
services/
├── api-gateway/
│   ├── conanfile.txt      # Conan dependencies
│   ├── CMakeLists.txt     # CMake build configuration
│   ├── main.cpp           # Service implementation
│   └── Dockerfile         # Multi-stage Docker build
└── (other services follow same pattern)
```

## Conan Configuration Files

### conanfile.txt

Defines project dependencies:

```ini
[requires]
grpc/1.48.0           # gRPC framework
protobuf/3.21.4       # Protocol Buffers
libcurl/7.86.0        # HTTP client library
openssl/1.1.1s        # SSL/TLS support
abseil/20220623.0     # Google's C++ library collection

[generators]
cmake_find_package    # Generate FindXXX.cmake files
cmake_paths          # Generate conan_paths.cmake
CMakeDeps            # Modern CMake dependencies
CMakeToolchain       # CMake toolchain file

[options]
grpc:cpp_plugin=True  # Enable C++ plugin for gRPC
grpc:shared=False     # Use static linking
protobuf:shared=False
libcurl:shared=False
libcurl:with_ssl=openssl
openssl:shared=False

[imports]
# Copy shared libraries to build directory
bin, *.dll -> ./bin
lib, *.so* -> ./lib
lib, *.dylib* -> ./lib
lib, *.a -> ./lib
```

### Key Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| gRPC | 1.48.0 | RPC framework for OTLP exporter |
| Protobuf | 3.21.4 | Serialization format |
| libcurl | 7.86.0 | HTTP client for service-to-service calls |
| OpenSSL | 1.1.1s | TLS/SSL support |
| Abseil | 20220623.0 | Required by gRPC |

**Note**: OpenTelemetry C++ is built from source as it's not yet in ConanCenter.

## CMake Integration

### CMakeLists.txt

The CMake configuration integrates with Conan:

```cmake
# Include Conan-generated files
if(EXISTS ${CMAKE_BINARY_DIR}/conan_paths.cmake)
    include(${CMAKE_BINARY_DIR}/conan_paths.cmake)
endif()

# Find packages (provided by Conan)
find_package(gRPC REQUIRED)
find_package(Protobuf REQUIRED)
find_package(CURL REQUIRED)

# Link against Conan packages
target_link_libraries(service
    PRIVATE
    gRPC::grpc++
    protobuf::libprotobuf
    CURL::libcurl
    opentelemetry-cpp::trace
    opentelemetry-cpp::otlp_grpc_exporter
)
```

## Docker Multi-Stage Build

### Stage 1: Builder

```dockerfile
# Install Conan
RUN pip3 install "conan==1.64.1"

# Configure Conan profile
RUN conan profile new default --detect --force && \
    conan profile update settings.compiler.libcxx=libstdc++11 default

# Install dependencies
RUN conan install .. --build=missing -s build_type=Release

# Build OpenTelemetry from source
RUN git clone ... opentelemetry-cpp && cmake && make install

# Build service
RUN cmake .. && make
```

### Stage 2: Runtime

```dockerfile
# Minimal runtime image
FROM ubuntu:22.04

# Copy binary and libraries
COPY --from=builder /app/build/service ./service
COPY --from=builder /app/build/lib ./lib

# Run service
CMD ["./service"]
```

## Build Process

### Local Development Build

```bash
# 1. Install Conan
pip3 install "conan==1.64.1"

# 2. Create Conan profile
conan profile new default --detect
conan profile update settings.compiler.libcxx=libstdc++11 default

# 3. Install dependencies
cd services/api-gateway
mkdir build && cd build
conan install .. --build=missing -s build_type=Release

# 4. Build with CMake
cmake .. -DCMAKE_BUILD_TYPE=Release
cmake --build . -j$(nproc)

# 5. Run
./service
```

### Docker Build

```bash
# Build single service
docker build -t api-gateway services/api-gateway/

# Build all services
docker-compose build

# Build without cache (fresh build)
docker-compose build --no-cache
```

## Troubleshooting

### Common Issues

#### 1. Conan Package Not Found

```
ERROR: Unable to find 'grpc/1.48.0' in remotes
```

**Solution**: Add ConanCenter remote
```bash
conan remote add conancenter https://center.conan.io
conan remote list  # Verify
```

#### 2. Compiler Version Mismatch

```
ERROR: Missing prebuilt package for 'grpc/1.48.0'
```

**Solution**: Build from source
```bash
conan install .. --build=missing
```

#### 3. Library Not Found at Runtime

```
error while loading shared libraries: libgrpc.so
```

**Solution**: Set library path
```bash
export LD_LIBRARY_PATH=/app/lib:$LD_LIBRARY_PATH
```

Or set RPATH in CMake:
```cmake
set_target_properties(service PROPERTIES
    INSTALL_RPATH "$ORIGIN/lib"
    BUILD_WITH_INSTALL_RPATH TRUE
)
```

#### 4. OpenSSL Version Conflicts

If you see OpenSSL-related errors, ensure consistent versions:

```ini
[requires]
openssl/1.1.1s  # Use specific version
libcurl:with_ssl=openssl
```

### Debug Commands

```bash
# List installed packages
conan search "*" --table

# Show package info
conan info grpc/1.48.0

# Clean Conan cache
conan remove "*" -f

# Verbose build output
conan install .. --build=missing -s build_type=Release -vv
```

## Performance Optimization

### Build Time Optimization

1. **Docker Layer Caching**: Copy `conanfile.txt` before source code
2. **Conan Cache**: Reuse `~/.conan/data` across builds
3. **Parallel Builds**: Use `-j$(nproc)` with CMake
4. **Binary Packages**: Conan downloads pre-compiled packages when available

### Build Time Comparison

| Approach | First Build | Subsequent Builds |
|----------|-------------|-------------------|
| Manual (from source) | 30-40 min | 25-35 min |
| **Conan (this project)** | **10-15 min** | **2-5 min** |

## Updating Dependencies

### Update Single Package

```ini
# In conanfile.txt
[requires]
grpc/1.50.0  # Update from 1.48.0
```

```bash
# Rebuild
docker-compose build --no-cache api-gateway
```

### Update All Packages

```bash
# Check for updates
conan search grpc --remote=conancenter

# Update conanfile.txt with new versions
# Then rebuild all services
docker-compose build
```

## Best Practices

### 1. Pin Exact Versions

✅ **Good**:
```ini
[requires]
grpc/1.48.0
```

❌ **Bad**:
```ini
[requires]
grpc/[>=1.48.0]  # May break compatibility
```

### 2. Use Static Linking for Services

```ini
[options]
grpc:shared=False
protobuf:shared=False
```

This creates self-contained binaries.

### 3. Consistent Conan Profiles

Ensure all services use the same profile:
```bash
conan profile update settings.compiler.libcxx=libstdc++11 default
```

### 4. Cache Conan Data in CI/CD

```yaml
# Example GitHub Actions
- uses: actions/cache@v3
  with:
    path: ~/.conan/data
    key: ${{ runner.os }}-conan-${{ hashFiles('**/conanfile.txt') }}
```

## Advanced Topics

### Custom Conan Packages

Create your own packages for internal libraries:

```python
# conanfile.py
from conan import ConanFile

class MyLibConan(ConanFile):
    name = "mylib"
    version = "1.0.0"
    # ...
```

### Conan Profiles for Different Environments

```bash
# Create production profile
conan profile new production --detect
conan profile update settings.build_type=Release production
conan profile update options.*:shared=False production

# Use profile
conan install .. --profile=production
```

## Further Reading

- [Conan Documentation](https://docs.conan.io/)
- [Conan Center](https://conan.io/center/)
- [CMake Integration](https://docs.conan.io/en/latest/integrations/build_system/cmake.html)
- [Docker Best Practices with Conan](https://docs.conan.io/en/latest/howtos/docker_builder.html)