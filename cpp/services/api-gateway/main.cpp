#include <arpa/inet.h>
#include <chrono>
#include <cstring>
#include <curl/curl.h>
#include <iostream>
#include <memory>
#include <netinet/in.h>
#include <string>
#include <sys/socket.h>
#include <thread>
#include <unistd.h>

#include "opentelemetry/common/key_value_iterable_view.h"
#include "opentelemetry/context/propagation/global_propagator.h"
#include "opentelemetry/context/propagation/text_map_propagator.h"
#include "opentelemetry/exporters/otlp/otlp_grpc_exporter_factory.h"
#include "opentelemetry/sdk/trace/simple_processor.h"
#include "opentelemetry/sdk/trace/tracer_provider.h"
#include "opentelemetry/trace/propagation/http_trace_context.h"
#include "opentelemetry/trace/provider.h"

namespace trace_api = opentelemetry::trace;
namespace trace_sdk = opentelemetry::sdk::trace;
namespace otlp = opentelemetry::exporter::otlp;
namespace context = opentelemetry::context;

// HTTP Header carrier for trace context propagation
class HttpHeaderCarrier : public context::propagation::TextMapCarrier {
public:
  HttpHeaderCarrier() = default;

  opentelemetry::nostd::string_view
  Get(opentelemetry::nostd::string_view key) const noexcept override {
    auto it = headers_.find(std::string(key));
    if (it != headers_.end()) {
      return it->second;
    }
    return "";
  }

  void Set(opentelemetry::nostd::string_view key,
           opentelemetry::nostd::string_view value) noexcept override {
    headers_[std::string(key)] = std::string(value);
  }

  const std::map<std::string, std::string> &GetHeaders() const {
    return headers_;
  }

private:
  std::map<std::string, std::string> headers_;
};

// CURL write callback
size_t WriteCallback(void *contents, size_t size, size_t nmemb, void *userp) {
  ((std::string *)userp)->append((char *)contents, size * nmemb);
  return size * nmemb;
}

// HTTP client with trace context propagation
std::string
httpGet(const std::string &url,
        const opentelemetry::nostd::shared_ptr<trace_api::Span> &span,
        const opentelemetry::nostd::shared_ptr<trace_api::Tracer> &tracer) {
  CURL *curl = curl_easy_init();
  std::string response;

  if (curl) {
    // Create carrier and inject trace context
    HttpHeaderCarrier carrier;
    auto propagator =
        context::propagation::GlobalTextMapPropagator::GetGlobalPropagator();
    auto current_context = context::RuntimeContext::GetCurrent();
    propagator->Inject(carrier, current_context);

    // Set CURL options
    curl_easy_setopt(curl, CURLOPT_URL, url.c_str());
    curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, WriteCallback);
    curl_easy_setopt(curl, CURLOPT_WRITEDATA, &response);

    // Add trace context headers
    struct curl_slist *headers = nullptr;
    for (const auto &[key, value] : carrier.GetHeaders()) {
      std::string header = key + ": " + value;
      headers = curl_slist_append(headers, header.c_str());
    }
    curl_easy_setopt(curl, CURLOPT_HTTPHEADER, headers);

    // Perform request
    CURLcode res = curl_easy_perform(curl);

    if (res == CURLE_OK) {
      span->SetAttribute("http.status_code", 200);
    } else {
      span->SetAttribute("http.status_code", 500);
      span->SetStatus(trace_api::StatusCode::kError, curl_easy_strerror(res));
    }

    curl_slist_free_all(headers);
    curl_easy_cleanup(curl);
  }

  return response;
}

// Initialize OpenTelemetry
void initTracer() {
  auto exporter = otlp::OtlpGrpcExporterFactory::Create();
  auto processor =
      std::make_unique<trace_sdk::SimpleSpanProcessor>(std::move(exporter));
  auto provider =
      std::make_shared<trace_sdk::TracerProvider>(std::move(processor));
  auto nostd_provider =
      opentelemetry::nostd::shared_ptr<trace_api::TracerProvider>(provider);
  trace_api::Provider::SetTracerProvider(nostd_provider);

  // Set global propagator for trace context
  context::propagation::GlobalTextMapPropagator::SetGlobalPropagator(
      opentelemetry::nostd::shared_ptr<context::propagation::TextMapPropagator>(
          new opentelemetry::trace::propagation::HttpTraceContext()));
}

// Simple HTTP server
void handleRequest(int client_socket) {
  char buffer[4096] = {0};
  read(client_socket, buffer, 4096);

  // Get tracer
  auto provider = trace_api::Provider::GetTracerProvider();
  auto tracer = provider->GetTracer("api-gateway", "1.0.0");

  // Start root span
  auto span =
      tracer->StartSpan("handle_request", {{"http.method", "GET"},
                                           {"http.scheme", "http"},
                                           {"http.target", "/api/order"}});

  auto scope = tracer->WithActiveSpan(span);

  std::string response_body;

  try {
    // Parse request to extract trace context
    std::string request(buffer);

    // Call order service
    auto order_span =
        tracer->StartSpan("call_order_service", {{"http.method", "GET"}});
    auto order_scope = tracer->WithActiveSpan(order_span);

    std::string order_url = "http://order-service:8081/order/123";
    std::string order_response = httpGet(order_url, order_span, tracer);

    order_span->AddEvent("order_service_responded");
    order_span->End();

    // Call user service
    auto user_span =
        tracer->StartSpan("call_user_service", {{"http.method", "GET"}});
    auto user_scope = tracer->WithActiveSpan(user_span);

    std::string user_url = "http://user-service:8082/user/456";
    std::string user_response = httpGet(user_url, user_span, tracer);

    user_span->AddEvent("user_service_responded");
    user_span->End();

    // Combine responses
    response_body =
        "{\"order\": " + order_response + ", \"user\": " + user_response + "}";

    span->SetStatus(trace_api::StatusCode::kOk);

  } catch (const std::exception &e) {
    span->SetStatus(trace_api::StatusCode::kError, e.what());
    response_body = "{\"error\": \"" + std::string(e.what()) + "\"}";
  }

  span->End();

  // Send HTTP response
  std::string http_response = "HTTP/1.1 200 OK\r\n"
                              "Content-Type: application/json\r\n"
                              "Content-Length: " +
                              std::to_string(response_body.length()) +
                              "\r\n"
                              "\r\n" +
                              response_body;

  write(client_socket, http_response.c_str(), http_response.length());
  close(client_socket);
}

int main() {
  std::cout << "API Gateway starting..." << std::endl;

  // Initialize CURL
  curl_global_init(CURL_GLOBAL_DEFAULT);

  // Initialize OpenTelemetry
  initTracer();

  // Create socket
  int server_fd = socket(AF_INET, SOCK_STREAM, 0);
  int opt = 1;
  setsockopt(server_fd, SOL_SOCKET, SO_REUSEADDR, &opt, sizeof(opt));

  struct sockaddr_in address;
  address.sin_family = AF_INET;
  address.sin_addr.s_addr = INADDR_ANY;
  address.sin_port = htons(8080);

  bind(server_fd, (struct sockaddr *)&address, sizeof(address));
  listen(server_fd, 10);

  std::cout << "API Gateway listening on port 8080" << std::endl;

  while (true) {
    int client_socket = accept(server_fd, nullptr, nullptr);
    if (client_socket >= 0) {
      std::thread(handleRequest, client_socket).detach();
    }
  }

  curl_global_cleanup();
  return 0;
}