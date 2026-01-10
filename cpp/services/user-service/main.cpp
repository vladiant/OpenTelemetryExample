#include <chrono>
#include <cstring>
#include <iostream>
#include <map>
#include <memory>
#include <netinet/in.h>
#include <random>
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

std::string extractUserId(const std::string &request) {
  size_t pos = request.find("/user/");
  if (pos != std::string::npos) {
    size_t start = pos + 6;
    size_t end = request.find(" ", start);
    if (end != std::string::npos) {
      return request.substr(start, end - start);
    }
  }
  return "unknown";
}

// Simulated user database
struct UserData {
  std::string id;
  std::string name;
  std::string email;
  std::string tier;
  int loyalty_points;
};

UserData getUserFromDatabase(
    const std::string &user_id,
    const opentelemetry::nostd::shared_ptr<trace_api::Span> &span,
    const opentelemetry::nostd::shared_ptr<trace_api::Tracer> &tracer) {
  // Create a child span for database operation
  auto db_span = tracer->StartSpan(
      "database.query", {{"db.system", "postgresql"},
                         {"db.operation", "SELECT"},
                         {"db.statement", "SELECT * FROM users WHERE id = ?"},
                         {"db.user", "service_account"}});

  auto scope = tracer->WithActiveSpan(db_span);

  db_span->AddEvent("query_start");

  // Simulate database latency
  std::random_device rd;
  std::mt19937 gen(rd());
  std::uniform_int_distribution<> latency(10, 50);
  std::this_thread::sleep_for(std::chrono::milliseconds(latency(gen)));

  db_span->AddEvent("query_complete");
  db_span->SetAttribute("db.rows_returned", 1);
  db_span->SetStatus(trace_api::StatusCode::kOk);
  db_span->End();

  // Return mock user data
  return UserData{user_id, "John Doe", "john.doe@example.com", "premium", 1250};
}

void validateUserPermissions(
    const std::string &user_id,
    const opentelemetry::nostd::shared_ptr<trace_api::Span> &span,
    const opentelemetry::nostd::shared_ptr<trace_api::Tracer> &tracer) {
  auto validation_span = tracer->StartSpan("validate_permissions");
  validation_span->SetAttribute("user.id", user_id);

  auto scope = tracer->WithActiveSpan(validation_span);

  validation_span->AddEvent("checking_permissions");
  std::this_thread::sleep_for(std::chrono::milliseconds(15));

  validation_span->SetAttribute("permissions.valid", true);
  validation_span->SetAttribute("permissions.level", "read_write");
  validation_span->SetStatus(trace_api::StatusCode::kOk);
  validation_span->End();
}

void handleRequest(int client_socket) {
  char buffer[4096] = {0};
  read(client_socket, buffer, 4096);

  std::string request(buffer);
  auto headers = parseHeaders(request);
  std::string user_id = extractUserId(request);

  // Extract trace context from incoming headers
  HttpHeaderCarrier carrier(headers);
  auto propagator =
      context::propagation::GlobalTextMapPropagator::GetGlobalPropagator();
  auto current_context = context::RuntimeContext::GetCurrent();
  auto extracted_context = propagator->Extract(carrier, current_context);

  auto provider = trace_api::Provider::GetTracerProvider();
  auto tracer = provider->GetTracer("user-service", "1.0.0");

  // Start span with extracted context as parent
  trace_api::StartSpanOptions options;
  options.parent = trace_api::GetSpan(extracted_context)->GetContext();

  auto span = tracer->StartSpan("get_user", options);
  span->SetAttribute("http.method", "GET");
  span->SetAttribute("http.target", std::string("/user/") + user_id);
  span->SetAttribute("user.id", user_id);

  auto scope = tracer->WithActiveSpan(span);

  std::string response_body;

  try {
    span->AddEvent("request_received", {{"user.id", user_id}});

    // Validate user permissions
    validateUserPermissions(user_id, span, tracer);

    // Get user data from database
    span->AddEvent("fetching_user_data");
    UserData user_data = getUserFromDatabase(user_id, span, tracer);

    // Check cache (simulated)
    auto cache_span = tracer->StartSpan("cache.lookup");
    cache_span->SetAttribute("cache.key", std::string("user:") + user_id);
    cache_span->SetAttribute("cache.system", "redis");
    auto cache_scope = tracer->WithActiveSpan(cache_span);

    std::this_thread::sleep_for(std::chrono::milliseconds(5));
    cache_span->SetAttribute("cache.hit", false);
    cache_span->End();

    // Build response
    span->AddEvent("building_response");
    response_body = "{"
                    "\"user_id\": \"" +
                    user_data.id +
                    "\", "
                    "\"name\": \"" +
                    user_data.name +
                    "\", "
                    "\"email\": \"" +
                    user_data.email +
                    "\", "
                    "\"tier\": \"" +
                    user_data.tier +
                    "\", "
                    "\"loyalty_points\": " +
                    std::to_string(user_data.loyalty_points) + "}";

    span->SetAttribute("user.tier", user_data.tier);
    span->SetAttribute("user.loyalty_points", user_data.loyalty_points);
    span->SetStatus(trace_api::StatusCode::kOk);

    std::cout << "User service: Processed request for user " << user_id
              << std::endl;

  } catch (const std::exception &e) {
    span->SetStatus(trace_api::StatusCode::kError, e.what());
    span->AddEvent("exception", {{"exception.type", typeid(e).name()},
                                 {"exception.message", e.what()}});
    response_body = "{\"error\": \"" + std::string(e.what()) + "\"}";
  }

  span->End();

  std::string http_response = "HTTP/1.1 200 OK\r\n"
                              "Content-Type: application/json\r\n"
                              "Content-Length: " +
                              std::to_string(response_body.length()) +
                              "\r\n"
                              "X-Service: user-service\r\n"
                              "\r\n" +
                              response_body;

  write(client_socket, http_response.c_str(), http_response.length());
  close(client_socket);
}

int main() {
  std::cout << "User Service starting..." << std::endl;

  initTracer();

  int server_fd = socket(AF_INET, SOCK_STREAM, 0);
  int opt = 1;
  setsockopt(server_fd, SOL_SOCKET, SO_REUSEADDR, &opt, sizeof(opt));

  struct sockaddr_in address;
  address.sin_family = AF_INET;
  address.sin_addr.s_addr = INADDR_ANY;
  address.sin_port = htons(8082);

  bind(server_fd, (struct sockaddr *)&address, sizeof(address));
  listen(server_fd, 10);

  std::cout << "User Service listening on port 8082" << std::endl;

  while (true) {
    int client_socket = accept(server_fd, nullptr, nullptr);
    if (client_socket >= 0) {
      std::thread(handleRequest, client_socket).detach();
    }
  }

  return 0;
}