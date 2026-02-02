#include <cstring>
#include <curl/curl.h>
#include <iostream>
#include <map>
#include <memory>
#include <netinet/in.h>
#include <string>
#include <sys/socket.h>
#include <thread>
#include <unistd.h>

#include "opentelemetry/common/key_value_iterable_view.h"
#include "opentelemetry/context/propagation/global_propagator.h"
#include "opentelemetry/exporters/otlp/otlp_grpc_exporter_factory.h"
#include "opentelemetry/sdk/trace/simple_processor.h"
#include "opentelemetry/sdk/trace/tracer_provider.h"
#include "opentelemetry/trace/propagation/http_trace_context.h"
#include "opentelemetry/trace/provider.h"

namespace trace_api = opentelemetry::trace;
namespace trace_sdk = opentelemetry::sdk::trace;
namespace otlp = opentelemetry::exporter::otlp;
namespace context = opentelemetry::context;

class HttpHeaderCarrier : public context::propagation::TextMapCarrier {
public:
  HttpHeaderCarrier() = default;
  explicit HttpHeaderCarrier(const std::map<std::string, std::string> &headers)
      : headers_(headers) {}

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

size_t WriteCallback(void *contents, size_t size, size_t nmemb, void *userp) {
  ((std::string *)userp)->append((char *)contents, size * nmemb);
  return size * nmemb;
}

std::string
httpGet(const std::string &url,
        const opentelemetry::nostd::shared_ptr<trace_api::Span> &span,
        const opentelemetry::nostd::shared_ptr<trace_api::Tracer> &tracer) {
  CURL *curl = curl_easy_init();
  std::string response;

  if (curl) {
    HttpHeaderCarrier carrier;
    auto propagator =
        context::propagation::GlobalTextMapPropagator::GetGlobalPropagator();
    auto current_ctx = context::RuntimeContext::GetCurrent();
    propagator->Inject(carrier, current_ctx);

    curl_easy_setopt(curl, CURLOPT_URL, url.c_str());
    curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, WriteCallback);
    curl_easy_setopt(curl, CURLOPT_WRITEDATA, &response);

    struct curl_slist *headers = nullptr;
    for (const auto &[key, value] : carrier.GetHeaders()) {
      std::string header = key + ": " + value;
      headers = curl_slist_append(headers, header.c_str());
    }
    curl_easy_setopt(curl, CURLOPT_HTTPHEADER, headers);

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

void initTracer() {
  auto exporter = otlp::OtlpGrpcExporterFactory::Create();
  auto processor =
      std::make_unique<trace_sdk::SimpleSpanProcessor>(std::move(exporter));
  auto provider =
      std::make_shared<trace_sdk::TracerProvider>(std::move(processor));
  auto nostd_provider =
      opentelemetry::nostd::shared_ptr<trace_api::TracerProvider>(provider);
  trace_api::Provider::SetTracerProvider(nostd_provider);

  context::propagation::GlobalTextMapPropagator::SetGlobalPropagator(
      opentelemetry::nostd::shared_ptr<context::propagation::TextMapPropagator>(
          new opentelemetry::trace::propagation::HttpTraceContext()));
}

std::map<std::string, std::string> parseHeaders(const std::string &request) {
  std::map<std::string, std::string> headers;
  size_t pos = 0;
  size_t end = request.find("\r\n\r\n");

  while (pos < end) {
    size_t line_end = request.find("\r\n", pos);
    if (line_end == std::string::npos)
      break;

    std::string line = request.substr(pos, line_end - pos);
    size_t colon = line.find(": ");

    if (colon != std::string::npos) {
      std::string key = line.substr(0, colon);
      std::string value = line.substr(colon + 2);
      headers[key] = value;
    }

    pos = line_end + 2;
  }

  return headers;
}

void handleRequest(int client_socket) {
  char buffer[4096] = {0};
  read(client_socket, buffer, 4096);

  std::string request(buffer);
  auto headers = parseHeaders(request);

  // Extract trace context from incoming headers
  HttpHeaderCarrier carrier(headers);
  auto propagator =
      context::propagation::GlobalTextMapPropagator::GetGlobalPropagator();
  auto current_context = context::RuntimeContext::GetCurrent();
  auto context = propagator->Extract(carrier, current_context);

  auto provider = trace_api::Provider::GetTracerProvider();
  auto tracer = provider->GetTracer("order-service", "1.0.0");

  // Start span with extracted context as parent
  trace_api::StartSpanOptions options;
  options.parent = trace_api::GetSpan(context)->GetContext();

  auto span = tracer->StartSpan("process_order",
                                {{"http.method", "GET"},
                                 {"http.target", "/order/123"},
                                 {"order.id", "123"}},
                                options);

  auto scope = tracer->WithActiveSpan(span);

  std::string response_body;

  try {
    // Simulate order processing
    span->AddEvent("validating_order");
    std::this_thread::sleep_for(std::chrono::milliseconds(50));

    // Call payment service
    auto payment_span = tracer->StartSpan("call_payment_service");
    auto payment_scope = tracer->WithActiveSpan(payment_span);

    std::string payment_url = "http://payment-service:8083/payment/123";
    std::string payment_response = httpGet(payment_url, payment_span, tracer);

    payment_span->End();

    // Call inventory service
    auto inventory_span = tracer->StartSpan("call_inventory_service");
    auto inventory_scope = tracer->WithActiveSpan(inventory_span);

    std::string inventory_url =
        "http://inventory-service:8084/inventory/item-456";
    std::string inventory_response =
        httpGet(inventory_url, inventory_span, tracer);

    inventory_span->End();

    span->AddEvent("order_completed");

    response_body = "{\"order_id\": \"123\", \"status\": \"completed\", "
                    "\"payment\": " +
                    payment_response +
                    ", "
                    "\"inventory\": " +
                    inventory_response + "}";

    span->SetAttribute("order.status", "completed");
    span->SetStatus(trace_api::StatusCode::kOk);

  } catch (const std::exception &e) {
    span->SetStatus(trace_api::StatusCode::kError, e.what());
    response_body = "{\"error\": \"" + std::string(e.what()) + "\"}";
  }

  span->End();

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
  std::cout << "Order Service starting..." << std::endl;

  curl_global_init(CURL_GLOBAL_DEFAULT);
  initTracer();

  int server_fd = socket(AF_INET, SOCK_STREAM, 0);
  int opt = 1;
  setsockopt(server_fd, SOL_SOCKET, SO_REUSEADDR, &opt, sizeof(opt));

  struct sockaddr_in address;
  address.sin_family = AF_INET;
  address.sin_addr.s_addr = INADDR_ANY;
  address.sin_port = htons(8081);

  bind(server_fd, (struct sockaddr *)&address, sizeof(address));
  listen(server_fd, 10);

  std::cout << "Order Service listening on port 8081" << std::endl;

  while (true) {
    int client_socket = accept(server_fd, nullptr, nullptr);
    if (client_socket >= 0) {
      std::thread(handleRequest, client_socket).detach();
    }
  }

  curl_global_cleanup();
  return 0;
}