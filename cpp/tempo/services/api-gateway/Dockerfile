FROM ubuntu:22.04 AS builder

ENV DEBIAN_FRONTEND=noninteractive

# Install build dependencies
RUN apt-get update && apt-get install -y \
    build-essential \
    cmake \
    git \
    pkg-config \
    libssl-dev \
    libcurl4-openssl-dev \
    libprotobuf-dev \
    protobuf-compiler \
    libgrpc++-dev \
    protobuf-compiler-grpc \
    python3 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /deps

# Install nlohmann/json
RUN git clone --depth 1 --branch v3.11.3 https://github.com/nlohmann/json.git && \
    cd json && mkdir build && cd build && \
    cmake .. -DJSON_BuildTests=OFF && \
    make -j$(nproc) && make install

# Install cpp-httplib
RUN git clone --depth 1 --branch v0.14.3 https://github.com/yhirose/cpp-httplib.git && \
    cd cpp-httplib && mkdir build && cd build && \
    cmake .. -DHTTPLIB_COMPILE=ON && \
    make -j$(nproc) && make install

# Install libcurl for HTTP exporter
RUN apt-get update && apt-get install -y libcurl4-openssl-dev && rm -rf /var/lib/apt/lists/*

# Install opentelemetry-cpp with HTTP exporter (simpler than gRPC)
RUN git clone --depth 1 --branch v1.14.2 https://github.com/open-telemetry/opentelemetry-cpp.git && \
    cd opentelemetry-cpp && mkdir build && cd build && \
    cmake .. \
        -DCMAKE_BUILD_TYPE=Release \
        -DBUILD_TESTING=OFF \
        -DWITH_OTLP_GRPC=OFF \
        -DWITH_OTLP_HTTP=ON \
        -DWITH_ABSEIL=OFF \
        -DCMAKE_POSITION_INDEPENDENT_CODE=ON && \
    make -j$(nproc) && make install

WORKDIR /app

# Copy all services (includes common headers)
COPY . /app/

WORKDIR /app/api-gateway

# Build the service
RUN mkdir build && cd build && \
    cmake .. -DCMAKE_BUILD_TYPE=Release && \
    make -j$(nproc)

# Runtime stage
FROM ubuntu:22.04

RUN apt-get update && apt-get install -y \
    libssl3 \
    libcurl4 \
    libprotobuf23 \
    && rm -rf /var/lib/apt/lists/*

# Copy OpenTelemetry shared libraries
COPY --from=builder /usr/local/lib/libopentelemetry*.so* /usr/local/lib/
RUN ldconfig

COPY --from=builder /app/api-gateway/build/api-gateway /usr/local/bin/

EXPOSE 8000

CMD ["api-gateway"]
