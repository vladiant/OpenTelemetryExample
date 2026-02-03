cmake_minimum_required(VERSION 3.16)
project(api-gateway)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Find required packages
find_package(opentelemetry-cpp CONFIG REQUIRED)
find_package(nlohmann_json CONFIG REQUIRED)
find_package(httplib CONFIG REQUIRED)
find_package(CURL REQUIRED)
find_package(Protobuf REQUIRED)

add_executable(api-gateway main.cpp)

target_include_directories(api-gateway PRIVATE
    ${CMAKE_SOURCE_DIR}/../common
)

target_link_libraries(api-gateway PRIVATE
    opentelemetry-cpp::trace
    opentelemetry-cpp::otlp_http_exporter
    nlohmann_json::nlohmann_json
    httplib::httplib
    CURL::libcurl
    protobuf::libprotobuf
)
