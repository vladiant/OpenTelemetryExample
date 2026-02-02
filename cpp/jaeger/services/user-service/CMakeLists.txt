cmake_minimum_required(VERSION 3.15)
project(otel-microservice)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Try to include Conan-generated files if they exist
if(EXISTS ${CMAKE_BINARY_DIR}/conan_paths.cmake)
    include(${CMAKE_BINARY_DIR}/conan_paths.cmake)
    message(STATUS "Using Conan paths")
endif()

if(EXISTS ${CMAKE_BINARY_DIR}/conan_toolchain.cmake)
    include(${CMAKE_BINARY_DIR}/conan_toolchain.cmake)
    message(STATUS "Using Conan toolchain")
endif()

set(CMAKE_MODULE_PATH ${CMAKE_BINARY_DIR} ${CMAKE_MODULE_PATH})
set(CMAKE_PREFIX_PATH ${CMAKE_BINARY_DIR} ${CMAKE_PREFIX_PATH})

# Find required packages
find_package(Threads REQUIRED)
find_package(CURL REQUIRED)
find_package(Protobuf REQUIRED)
find_package(gRPC REQUIRED)
find_package(absl REQUIRED)
find_package(nlohmann_json CONFIG REQUIRED)
find_package(protobuf REQUIRED)

# Find OpenTelemetry (installed system-wide)
find_package(opentelemetry-cpp REQUIRED)

# Include directories
include_directories(
    ${CURL_INCLUDE_DIRS}
)

# Create executable
add_executable(service
    main.cpp
)

# Link libraries
target_link_libraries(service
    PRIVATE
    ${CURL_LIBRARIES}
    Threads::Threads
    opentelemetry-cpp::trace
    opentelemetry-cpp::otlp_grpc_exporter
    opentelemetry-cpp::otlp_http_exporter
    opentelemetry-cpp::http_client_curl
)

# Set compile options
target_compile_options(service PRIVATE -Wall -Wextra)

# Set RPATH for runtime library discovery
set_target_properties(service PROPERTIES
    INSTALL_RPATH "$ORIGIN:$ORIGIN/lib:/usr/local/lib"
    BUILD_RPATH "$ORIGIN:$ORIGIN/lib:/usr/local/lib"
    BUILD_WITH_INSTALL_RPATH TRUE
)