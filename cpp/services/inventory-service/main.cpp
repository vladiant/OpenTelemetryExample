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
#include <vector>

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

std::string extractItemId(const std::string &request) {
  size_t pos = request.find("/inventory/");
  if (pos != std::string::npos) {
    size_t start = pos + 11;
    size_t end = request.find(" ", start);
    if (end != std::string::npos) {
      return request.substr(start, end - start);
    }
  }
  return "unknown";
}

// Inventory data structure
struct InventoryItem {
  std::string item_id;
  std::string name;
  int quantity;
  std::string warehouse;
  std::string status;
};

InventoryItem checkDatabaseStock(
    const std::string &item_id,
    const opentelemetry::nostd::shared_ptr<trace_api::Span> &span,
    const opentelemetry::nostd::shared_ptr<trace_api::Tracer> &tracer) {
  auto db_span = tracer->StartSpan(
      "database.query",
      {{"db.system", "postgresql"},
       {"db.operation", "SELECT"},
       {"db.table", "inventory"},
       {"db.statement", "SELECT * FROM inventory WHERE item_id = ?"}});

  auto scope = tracer->WithActiveSpan(db_span);

  db_span->AddEvent("executing_query");

  // Simulate database query latency
  std::random_device rd;
  std::mt19937 gen(rd());
  std::uniform_int_distribution<> latency(15, 45);
  std::this_thread::sleep_for(std::chrono::milliseconds(latency(gen)));

  db_span->AddEvent("query_completed");
  db_span->SetAttribute("db.rows_returned", 1);
  db_span->SetStatus(trace_api::StatusCode::kOk);
  db_span->End();

  // Return simulated inventory data
  std::uniform_int_distribution<> stock_qty(0, 150);
  return InventoryItem{item_id, "Premium Widget", stock_qty(gen),
                       "warehouse-east-1", "available"};
}

std::vector<std::string> checkWarehouseLocations(
    const std::string &item_id,
    const opentelemetry::nostd::shared_ptr<trace_api::Span> &span,
    const opentelemetry::nostd::shared_ptr<trace_api::Tracer> &tracer) {
  auto warehouse_span =
      tracer->StartSpan("check_warehouse_locations", {{"item.id", item_id}});

  auto scope = tracer->WithActiveSpan(warehouse_span);

  std::vector<std::string> warehouses = {"warehouse-east-1", "warehouse-west-2",
                                         "warehouse-central"};

  warehouse_span->AddEvent("querying_warehouse_system");
  std::this_thread::sleep_for(std::chrono::milliseconds(20));

  // Simulate checking multiple warehouses
  std::random_device rd;
  std::mt19937 gen(rd());
  std::uniform_int_distribution<> has_stock(0, 1);

  std::vector<std::string> available_warehouses;
  for (const auto &warehouse : warehouses) {
    auto check_span =
        tracer->StartSpan("check_warehouse", {{"warehouse.name", warehouse}});

    std::this_thread::sleep_for(std::chrono::milliseconds(10));

    bool in_stock = has_stock(gen) == 1;
    check_span->SetAttribute("warehouse.has_stock", in_stock);

    if (in_stock) {
      available_warehouses.push_back(warehouse);
      check_span->AddEvent("stock_found");
    }

    check_span->End();
  }

  warehouse_span->SetAttribute("warehouses.checked",
                               static_cast<int>(warehouses.size()));
  warehouse_span->SetAttribute("warehouses.available",
                               static_cast<int>(available_warehouses.size()));
  warehouse_span->End();

  return available_warehouses;
}

bool reserveInventory(
    const std::string &item_id, int quantity,
    const opentelemetry::nostd::shared_ptr<trace_api::Span> &span,
    const opentelemetry::nostd::shared_ptr<trace_api::Tracer> &tracer) {
  auto reserve_span = tracer->StartSpan(
      "reserve_inventory",
      {{"item.id", item_id}, {"quantity", std::to_string(quantity)}});

  auto scope = tracer->WithActiveSpan(reserve_span);

  reserve_span->AddEvent("creating_reservation");

  // Simulate reservation logic
  std::this_thread::sleep_for(std::chrono::milliseconds(30));

  // Create reservation record
  auto db_span =
      tracer->StartSpan("database.insert", {{"db.system", "postgresql"},
                                            {"db.operation", "INSERT"},
                                            {"db.table", "reservations"}});

  std::this_thread::sleep_for(std::chrono::milliseconds(20));
  db_span->SetAttribute("db.rows_affected", 1);
  db_span->End();

  reserve_span->AddEvent("reservation_created");
  reserve_span->SetAttribute("reservation.status", "confirmed");
  reserve_span->SetStatus(trace_api::StatusCode::kOk);
  reserve_span->End();

  return true;
}

void updateInventoryCache(
    const std::string &item_id, const InventoryItem &item,
    const opentelemetry::nostd::shared_ptr<trace_api::Span> &span,
    const opentelemetry::nostd::shared_ptr<trace_api::Tracer> &tracer) {
  auto cache_span = tracer->StartSpan("cache.update");
  cache_span->SetAttribute("cache.system", "redis");
  cache_span->SetAttribute("cache.key", std::string("inventory:") + item_id);

  auto scope = tracer->WithActiveSpan(cache_span);

  cache_span->AddEvent("writing_to_cache");
  std::this_thread::sleep_for(std::chrono::milliseconds(8));

  cache_span->SetAttribute("cache.ttl", 300); // 5 minutes
  cache_span->SetStatus(trace_api::StatusCode::kOk);
  cache_span->End();
}

void handleRequest(int client_socket) {
  char buffer[4096] = {0};
  read(client_socket, buffer, 4096);

  std::string request(buffer);
  auto headers = parseHeaders(request);
  std::string item_id = extractItemId(request);

  // Extract trace context from incoming headers
  HttpHeaderCarrier carrier(headers);
  auto propagator =
      context::propagation::GlobalTextMapPropagator::GetGlobalPropagator();
  auto current_context = context::RuntimeContext::GetCurrent();
  auto extracted_context = propagator->Extract(carrier, current_context);

  auto provider = trace_api::Provider::GetTracerProvider();
  auto tracer = provider->GetTracer("inventory-service", "1.0.0");

  // Start span with extracted context as parent
  trace_api::StartSpanOptions options;
  options.parent = trace_api::GetSpan(extracted_context)->GetContext();

  auto span = tracer->StartSpan("check_inventory", options);
  span->SetAttribute("http.method", "GET");
  span->SetAttribute("http.target", std::string("/inventory/") + item_id);
  span->SetAttribute("item.id", item_id);

  auto scope = tracer->WithActiveSpan(span);

  std::string response_body;

  try {
    span->AddEvent("inventory_check_started");

    // Step 1: Check database for stock
    span->AddEvent("checking_stock_levels");
    InventoryItem item = checkDatabaseStock(item_id, span, tracer);

    span->SetAttribute("item.quantity", item.quantity);
    span->SetAttribute("item.warehouse", item.warehouse);

    // Step 2: Check warehouse locations
    span->AddEvent("checking_warehouse_availability");
    auto warehouses = checkWarehouseLocations(item_id, span, tracer);

    // Step 3: Reserve inventory if available
    bool reserved = false;
    if (item.quantity > 0) {
      span->AddEvent("reserving_inventory");
      reserved = reserveInventory(item_id, 1, span, tracer);
    }

    // Step 4: Update cache
    span->AddEvent("updating_cache");
    updateInventoryCache(item_id, item, span, tracer);

    // Build warehouse list JSON
    std::string warehouse_list = "[";
    for (size_t i = 0; i < warehouses.size(); ++i) {
      warehouse_list += "\"" + warehouses[i] + "\"";
      if (i < warehouses.size() - 1)
        warehouse_list += ", ";
    }
    warehouse_list += "]";

    // Determine availability status
    std::string availability = item.quantity > 0 ? "in_stock" : "out_of_stock";
    span->SetAttribute("inventory.status", availability);

    span->AddEvent("inventory_check_completed");

    response_body = "{"
                    "\"item_id\": \"" +
                    item.item_id +
                    "\", "
                    "\"name\": \"" +
                    item.name +
                    "\", "
                    "\"quantity\": " +
                    std::to_string(item.quantity) +
                    ", "
                    "\"status\": \"" +
                    availability +
                    "\", "
                    "\"reserved\": " +
                    (reserved ? "true" : "false") +
                    ", "
                    "\"primary_warehouse\": \"" +
                    item.warehouse +
                    "\", "
                    "\"available_warehouses\": " +
                    warehouse_list + "}";

    span->SetStatus(trace_api::StatusCode::kOk);

    std::cout << "Inventory service: Checked item " << item_id
              << " - Quantity: " << item.quantity
              << " - Status: " << availability << std::endl;

  } catch (const std::exception &e) {
    span->SetStatus(trace_api::StatusCode::kError, e.what());
    span->AddEvent("inventory_check_failed",
                   {{"exception.type", typeid(e).name()},
                    {"exception.message", e.what()}});

    response_body = "{"
                    "\"item_id\": \"" +
                    item_id +
                    "\", "
                    "\"status\": \"error\", "
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
                              "X-Service: inventory-service\r\n"
                              "\r\n" +
                              response_body;

  write(client_socket, http_response.c_str(), http_response.length());
  close(client_socket);
}

int main() {
  std::cout << "Inventory Service starting..." << std::endl;

  initTracer();

  int server_fd = socket(AF_INET, SOCK_STREAM, 0);
  int opt = 1;
  setsockopt(server_fd, SOL_SOCKET, SO_REUSEADDR, &opt, sizeof(opt));

  struct sockaddr_in address;
  address.sin_family = AF_INET;
  address.sin_addr.s_addr = INADDR_ANY;
  address.sin_port = htons(8084);

  bind(server_fd, (struct sockaddr *)&address, sizeof(address));
  listen(server_fd, 10);

  std::cout << "Inventory Service listening on port 8084" << std::endl;

  while (true) {
    int client_socket = accept(server_fd, nullptr, nullptr);
    if (client_socket >= 0) {
      std::thread(handleRequest, client_socket).detach();
    }
  }

  return 0;
}