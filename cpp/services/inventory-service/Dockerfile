# Base image with build tools
FROM ubuntu:22.04 as builder

ENV DEBIAN_FRONTEND=noninteractive

# Install build dependencies
RUN apt-get update && apt-get install -y \
    build-essential \
    cmake \
    git \
    wget \
    python3 \
    python3-pip \
    pkg-config \
    autoconf \
    libtool \
    libcurl4-openssl-dev \
    libssl-dev \
    && rm -rf /var/lib/apt/lists/*

# Install Conan (for dependency management tracking)
RUN pip3 install --no-cache-dir "conan==1.64.1"

# Set up workspace
WORKDIR /build_deps

# Build Abseil
RUN git clone --depth 1 --branch 20230125.3 https://github.com/abseil/abseil-cpp.git && \
    cd abseil-cpp && mkdir build && cd build && \
    cmake .. \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_POSITION_INDEPENDENT_CODE=TRUE \
    -DCMAKE_INSTALL_PREFIX=/usr/local \
    -DABSL_PROPAGATE_CXX_STD=ON \
    -DABSL_USE_EXTERNAL_GOOGLETEST=OFF \
    -DABSL_BUILD_TESTING=OFF && \
    cmake --build . -j$(nproc) && \
    cmake --install .

# Build RE2
RUN git clone --depth 1 --branch 2023-03-01 https://github.com/google/re2.git && \
    cd re2 && mkdir build && cd build && \
    cmake .. \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_POSITION_INDEPENDENT_CODE=TRUE \
    -DCMAKE_INSTALL_PREFIX=/usr/local && \
    cmake --build . -j$(nproc) && \
    cmake --install .

# Build Protobuf
RUN git clone --depth 1 --branch v21.12 https://github.com/protocolbuffers/protobuf.git && \
    cd protobuf && git submodule update --init --recursive && \
    mkdir build && cd build && \
    cmake ../cmake \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_POSITION_INDEPENDENT_CODE=TRUE \
    -DCMAKE_INSTALL_PREFIX=/usr/local \
    -Dprotobuf_BUILD_TESTS=OFF \
    -Dprotobuf_ABSL_PROVIDER=package && \
    cmake --build . -j$(nproc) && \
    cmake --install .

# Build gRPC
RUN git clone --depth 1 --branch v1.54.2 https://github.com/grpc/grpc.git && \
    cd grpc && git submodule update --init && \
    mkdir build && cd build && \
    cmake .. \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_INSTALL_PREFIX=/usr/local \
    -DCMAKE_POSITION_INDEPENDENT_CODE=TRUE \
    -DgRPC_INSTALL=ON \
    -DgRPC_BUILD_TESTS=OFF \
    -DgRPC_ABSL_PROVIDER=package \
    -DgRPC_PROTOBUF_PROVIDER=package \
    -DgRPC_RE2_PROVIDER=package \
    -DgRPC_SSL_PROVIDER=package \
    -DgRPC_ZLIB_PROVIDER=package && \
    cmake --build . -j$(nproc) && \
    cmake --install .

# Update ldconfig
RUN ldconfig

# Build OpenTelemetry C++
RUN git clone --recurse-submodules --depth 1 --branch v1.14.2 \
    https://github.com/open-telemetry/opentelemetry-cpp.git && \
    cd opentelemetry-cpp && mkdir build && cd build && \
    cmake .. \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_INSTALL_PREFIX=/usr/local \
    -DCMAKE_POSITION_INDEPENDENT_CODE=TRUE \
    -DBUILD_TESTING=OFF \
    -DWITH_OTLP_GRPC=ON \
    -DWITH_OTLP_HTTP=ON \
    -DWITH_ZIPKIN=OFF \
    -DWITH_JAEGER=OFF \
    -DWITH_PROMETHEUS=OFF && \
    cmake --build . -j$(nproc) && \
    cmake --install .

RUN ldconfig

# Build the service
WORKDIR /app
COPY CMakeLists.txt main.cpp ./

RUN mkdir build && cd build && \
    cmake .. -DCMAKE_BUILD_TYPE=Release && \
    cmake --build . -j$(nproc)

# Runtime image
FROM ubuntu:22.04

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && apt-get install -y \
    libcurl4 \
    libssl3 \
    libstdc++6 \
    && rm -rf /var/lib/apt/lists/*

# Copy all libraries from builder
COPY --from=builder /usr/local/lib/ /usr/local/lib/
COPY --from=builder /app/build/service /app/service

RUN ldconfig

WORKDIR /app
CMD ["./service"]