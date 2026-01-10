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

std::string extractPaymentId(const std::string &request) {
  size_t pos = request.find("/payment/");
  if (pos != std::string::npos) {
    size_t start = pos + 9;
    size_t end = request.find(" ", start);
    if (end != std::string::npos) {
      return request.substr(start, end - start);
    }
  }
  return "unknown";
}

// Payment processing functions
bool validatePaymentMethod(
    const std::string &payment_id,
    const opentelemetry::nostd::shared_ptr<trace_api::Span> &span,
    const opentelemetry::nostd::shared_ptr<trace_api::Tracer> &tracer) {
  auto validation_span = tracer->StartSpan("validate_payment_method",
                                           {{"payment.id", payment_id}});

  auto scope = tracer->WithActiveSpan(validation_span);

  validation_span->AddEvent("checking_card_details");
  std::this_thread::sleep_for(std::chrono::milliseconds(20));

  // Simulate validation logic
  validation_span->SetAttribute("payment.method", "credit_card");
  validation_span->SetAttribute("card.type", "visa");
  validation_span->SetAttribute("card.last4", "4242");
  validation_span->SetAttribute("validation.result", true);

  validation_span->SetStatus(trace_api::StatusCode::kOk);
  validation_span->End();

  return true;
}

bool checkFraudDetection(
    const std::string &payment_id, double amount,
    const opentelemetry::nostd::shared_ptr<trace_api::Span> &span,
    const opentelemetry::nostd::shared_ptr<trace_api::Tracer> &tracer) {
  auto fraud_span = tracer->StartSpan(
      "fraud_detection", {{"payment.id", payment_id},
                          {"payment.amount", std::to_string(amount)},
                          {"fraud.system", "ml_model_v2"}});

  auto scope = tracer->WithActiveSpan(fraud_span);

  fraud_span->AddEvent("analyzing_transaction_patterns");

  // Simulate ML model inference time
  std::random_device rd;
  std::mt19937 gen(rd());
  std::uniform_int_distribution<> latency(30, 80);
  std::this_thread::sleep_for(std::chrono::milliseconds(latency(gen)));

  // Calculate fraud score (simulated)
  std::uniform_real_distribution<> score_dist(0.0, 0.3);
  double fraud_score = score_dist(gen);

  fraud_span->SetAttribute("fraud.score", fraud_score);
  fraud_span->SetAttribute("fraud.threshold", 0.75);
  fraud_span->SetAttribute("fraud.detected", false);

  fraud_span->AddEvent("fraud_check_complete",
                       {{"score", std::to_string(fraud_score)}});

  fraud_span->SetStatus(trace_api::StatusCode::kOk);
  fraud_span->End();

  return fraud_score < 0.75;
}

std::string processPaymentGateway(
    const std::string &payment_id, double amount,
    const opentelemetry::nostd::shared_ptr<trace_api::Span> &span,
    const opentelemetry::nostd::shared_ptr<trace_api::Tracer> &tracer) {
  auto gateway_span = tracer->StartSpan(
      "payment_gateway.authorize", {{"payment.gateway", "stripe"},
                                    {"payment.id", payment_id},
                                    {"payment.amount", std::to_string(amount)},
                                    {"payment.currency", "USD"}});

  auto scope = tracer->WithActiveSpan(gateway_span);

  gateway_span->AddEvent("sending_authorization_request");

  // Simulate gateway API call
  std::random_device rd;
  std::mt19937 gen(rd());
  std::uniform_int_distribution<> latency(50, 150);
  std::this_thread::sleep_for(std::chrono::milliseconds(latency(gen)));

  // Generate transaction ID
  std::string transaction_id = "txn_" + std::to_string(gen());

  gateway_span->SetAttribute("transaction.id", transaction_id);
  gateway_span->SetAttribute("gateway.response_code", "approved");
  gateway_span->SetAttribute("gateway.authorization_code", "AUTH123456");

  gateway_span->AddEvent("authorization_approved",
                         {{"transaction_id", transaction_id}});

  gateway_span->SetStatus(trace_api::StatusCode::kOk);
  gateway_span->End();

  return transaction_id;
}

void recordPaymentToDatabase(
    const std::string &payment_id, const std::string &transaction_id,
    const opentelemetry::nostd::shared_ptr<trace_api::Span> &span,
    const opentelemetry::nostd::shared_ptr<trace_api::Tracer> &tracer) {
  auto db_span = tracer->StartSpan(
      "database.insert",
      {{"db.system", "postgresql"},
       {"db.operation", "INSERT"},
       {"db.table", "payments"},
       {"db.statement",
        "INSERT INTO payments (id, transaction_id, status) VALUES (?, ?, ?)"}});

  auto scope = tracer->WithActiveSpan(db_span);

  db_span->AddEvent("writing_payment_record");
  std::this_thread::sleep_for(std::chrono::milliseconds(25));

  db_span->SetAttribute("db.rows_affected", 1);
  db_span->SetStatus(trace_api::StatusCode::kOk);
  db_span->End();
}

void handleRequest(int client_socket) {
  char buffer[4096] = {0};
  read(client_socket, buffer, 4096);

  std::string request(buffer);
  auto headers = parseHeaders(request);
  std::string payment_id = extractPaymentId(request);

  // Extract trace context from incoming headers
  HttpHeaderCarrier carrier(headers);
  auto propagator =
      context::propagation::GlobalTextMapPropagator::GetGlobalPropagator();
  auto current_context = context::RuntimeContext::GetCurrent();
  auto extracted_context = propagator->Extract(carrier, current_context);

  auto provider = trace_api::Provider::GetTracerProvider();
  auto tracer = provider->GetTracer("payment-service", "1.0.0");

  // Start span with extracted context as parent
  trace_api::StartSpanOptions options;
  options.parent = trace_api::GetSpan(extracted_context)->GetContext();

  auto span = tracer->StartSpan("process_payment",
                                {{"http.method", "GET"},
                                 {"http.target", "/payment/" + payment_id},
                                 {"payment.id", payment_id}},
                                options);

  auto scope = tracer->WithActiveSpan(span);

  std::string response_body;

  try {
    span->AddEvent("payment_processing_started");

    // Simulated payment amount
    double amount = 149.99;
    span->SetAttribute("payment.amount", amount);
    span->SetAttribute("payment.currency", "USD");

    // Step 1: Validate payment method
    span->AddEvent("validating_payment_method");
    bool is_valid = validatePaymentMethod(payment_id, span, tracer);

    if (!is_valid) {
      throw std::runtime_error("Invalid payment method");
    }

    // Step 2: Fraud detection
    span->AddEvent("running_fraud_detection");
    bool is_safe = checkFraudDetection(payment_id, amount, span, tracer);

    if (!is_safe) {
      span->SetAttribute("payment.status", "declined_fraud");
      throw std::runtime_error("Payment declined due to fraud detection");
    }

    // Step 3: Process through payment gateway
    span->AddEvent("authorizing_payment");
    std::string transaction_id =
        processPaymentGateway(payment_id, amount, span, tracer);

    // Step 4: Record to database
    span->AddEvent("recording_payment");
    recordPaymentToDatabase(payment_id, transaction_id, span, tracer);

    // Success
    span->AddEvent("payment_completed");
    span->SetAttribute("payment.status", "approved");
    span->SetAttribute("transaction.id", transaction_id);

    response_body = "{"
                    "\"payment_id\": \"" +
                    payment_id +
                    "\", "
                    "\"transaction_id\": \"" +
                    transaction_id +
                    "\", "
                    "\"status\": \"approved\", "
                    "\"amount\": " +
                    std::to_string(amount) +
                    ", "
                    "\"currency\": \"USD\""
                    "}";

    span->SetStatus(trace_api::StatusCode::kOk);

    std::cout << "Payment service: Processed payment " << payment_id
              << " - Transaction: " << transaction_id << std::endl;

  } catch (const std::exception &e) {
    span->SetStatus(trace_api::StatusCode::kError, e.what());
    span->AddEvent("payment_failed", {{"exception.type", typeid(e).name()},
                                      {"exception.message", e.what()}});

    response_body = "{"
                    "\"payment_id\": \"" +
                    payment_id +
                    "\", "
                    "\"status\": \"failed\", "
                    "\"error\": \"" +
                    std::string(e.what()) +
                    "\""
                    "}";
  }

  span->End();

  std::string http_response = "HTTP/1.1 200 OK\r\n"
                              "Content-Type: application/json\r\n"
                              "Content-Length: " +
                              std::to_string(response_body.length()) +
                              "\r\n"
                              "X-Service: payment-service\r\n"
                              "\r\n" +
                              response_body;

  write(client_socket, http_response.c_str(), http_response.length());
  close(client_socket);
}

int main() {
  std::cout << "Payment Service starting..." << std::endl;

  initTracer();

  int server_fd = socket(AF_INET, SOCK_STREAM, 0);
  int opt = 1;
  setsockopt(server_fd, SOL_SOCKET, SO_REUSEADDR, &opt, sizeof(opt));

  struct sockaddr_in address;
  address.sin_family = AF_INET;
  address.sin_addr.s_addr = INADDR_ANY;
  address.sin_port = htons(8083);

  bind(server_fd, (struct sockaddr *)&address, sizeof(address));
  listen(server_fd, 10);

  std::cout << "Payment Service listening on port 8083" << std::endl;

  while (true) {
    int client_socket = accept(server_fd, nullptr, nullptr);
    if (client_socket >= 0) {
      std::thread(handleRequest, client_socket).detach();
    }
  }

  return 0;
}