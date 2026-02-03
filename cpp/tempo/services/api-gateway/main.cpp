/**
 * API Gateway Service - Entry point for all client requests.
 * 
 * This service demonstrates:
 * - Automatic span creation for HTTP endpoints
 * - Manual span creation for business logic
 * - Context propagation to downstream services
 */

#include <iostream>
#include <string>
#include <map>
#include <thread>
#include <chrono>

#define CPPHTTPLIB_OPENSSL_SUPPORT
#include <httplib.h>
#include <nlohmann/json.hpp>

#include "../common/tracing.hpp"
#include "../common/http_client.hpp"

using json = nlohmann::json;

// Extract trace context from request headers
std::map<std::string, std::string> get_headers(const httplib::Request& req) {
    std::map<std::string, std::string> headers;
    for (const auto& [key, value] : req.headers) {
        headers[key] = value;
    }
    return headers;
}

int main() {
    std::string service_name = "api-gateway";
    if (const char* env = std::getenv("SERVICE_NAME")) {
        service_name = env;
    }
    
    std::string otlp_endpoint = "localhost:4317";
    if (const char* env = std::getenv("OTEL_EXPORTER_OTLP_ENDPOINT")) {
        otlp_endpoint = env;
    }
    
    std::string order_host = "localhost";
    int order_port = 8001;
    if (const char* env = std::getenv("ORDER_SERVICE_HOST")) {
        order_host = env;
    }
    if (const char* env = std::getenv("ORDER_SERVICE_PORT")) {
        order_port = std::stoi(env);
    }
    
    std::string inventory_host = "localhost";
    int inventory_port = 8002;
    if (const char* env = std::getenv("INVENTORY_SERVICE_HOST")) {
        inventory_host = env;
    }
    if (const char* env = std::getenv("INVENTORY_SERVICE_PORT")) {
        inventory_port = std::stoi(env);
    }
    
    // Initialize tracing
    tracing::init_tracing(service_name, otlp_endpoint);
    auto tracer = tracing::get_tracer(service_name);
    
    std::cout << "Tracing configured for " << service_name << " -> " << otlp_endpoint << std::endl;
    
    httplib::Server svr;
    
    // Health check
    svr.Get("/", [](const httplib::Request&, httplib::Response& res) {
        json response = {{"service", "api-gateway"}, {"status", "healthy"}};
        res.set_content(response.dump(), "application/json");
    });
    
    svr.Get("/health", [&order_host, &order_port, &inventory_host, &inventory_port](const httplib::Request&, httplib::Response& res) {
        json response = {
            {"service", "api-gateway"},
            {"status", "healthy"},
            {"dependencies", {
                {"order_service", "http://" + order_host + ":" + std::to_string(order_port)},
                {"inventory_service", "http://" + inventory_host + ":" + std::to_string(inventory_port)}
            }}
        };
        res.set_content(response.dump(), "application/json");
    });
    
    // Create order
    svr.Post("/orders", [&tracer, &order_host, &order_port](const httplib::Request& req, httplib::Response& res) {
        auto span = tracer->StartSpan("POST /orders");
        auto scope = tracer->WithActiveSpan(span);
        
        json body;
        try {
            body = json::parse(req.body);
        } catch (...) {
            body = {{"product_id", "demo-product"}, {"quantity", 1}};
        }
        
        // Create a custom span for business logic
        auto process_span = tracer->StartSpan("process_order_request");
        process_span->SetAttribute("order.product_id", body.value("product_id", "unknown"));
        process_span->SetAttribute("order.quantity", static_cast<int64_t>(body.value("quantity", 0)));
        
        std::cout << "Processing order request: " << body.dump() << std::endl;
        
        http_client::TracedHttpClient order_client(order_host, order_port);
        auto response = order_client.post("/orders", body, "HTTP POST order-service");
        
        if (!response.success) {
            process_span->SetStatus(opentelemetry::trace::StatusCode::kError, "Order creation failed");
            process_span->End();
            span->SetStatus(opentelemetry::trace::StatusCode::kError, "Order creation failed");
            span->End();
            
            res.status = response.status > 0 ? response.status : 503;
            res.set_content(response.body.empty() ? R"({"detail": "Order service unavailable"})" : response.body, "application/json");
            return;
        }
        
        if (response.json_body.contains("order_id")) {
            process_span->SetAttribute("order.id", response.json_body["order_id"].get<std::string>());
        }
        
        process_span->End();
        span->End();
        
        res.set_content(response.body, "application/json");
    });
    
    // Get order by ID
    svr.Get(R"(/orders/([^/]+))", [&tracer, &order_host, &order_port](const httplib::Request& req, httplib::Response& res) {
        std::string order_id = req.matches[1];
        
        auto span = tracer->StartSpan("GET /orders/{order_id}");
        auto scope = tracer->WithActiveSpan(span);
        span->SetAttribute("order.id", order_id);
        
        http_client::TracedHttpClient order_client(order_host, order_port);
        auto response = order_client.get("/orders/" + order_id, "HTTP GET order-service");
        
        span->End();
        
        res.status = response.status > 0 ? response.status : 503;
        res.set_content(response.body.empty() ? R"({"detail": "Order service unavailable"})" : response.body, "application/json");
    });
    
    // Get all inventory
    svr.Get("/inventory", [&tracer, &inventory_host, &inventory_port](const httplib::Request&, httplib::Response& res) {
        auto span = tracer->StartSpan("GET /inventory");
        auto scope = tracer->WithActiveSpan(span);
        
        http_client::TracedHttpClient inventory_client(inventory_host, inventory_port);
        auto response = inventory_client.get("/inventory", "HTTP GET inventory-service");
        
        if (response.success && response.json_body.is_array()) {
            span->SetAttribute("inventory.item_count", static_cast<int64_t>(response.json_body.size()));
        }
        
        span->End();
        
        res.status = response.status > 0 ? response.status : 503;
        res.set_content(response.body.empty() ? R"({"detail": "Inventory service unavailable"})" : response.body, "application/json");
    });
    
    // Get inventory for specific product
    svr.Get(R"(/inventory/([^/]+))", [&tracer, &inventory_host, &inventory_port](const httplib::Request& req, httplib::Response& res) {
        std::string product_id = req.matches[1];
        
        auto span = tracer->StartSpan("GET /inventory/{product_id}");
        auto scope = tracer->WithActiveSpan(span);
        span->SetAttribute("product.id", product_id);
        
        http_client::TracedHttpClient inventory_client(inventory_host, inventory_port);
        auto response = inventory_client.get("/inventory/" + product_id, "HTTP GET inventory-service");
        
        span->End();
        
        res.status = response.status > 0 ? response.status : 503;
        res.set_content(response.body.empty() ? R"({"detail": "Inventory service unavailable"})" : response.body, "application/json");
    });
    
    std::cout << "API Gateway starting on port 8000..." << std::endl;
    svr.listen("0.0.0.0", 8000);
    
    tracing::cleanup_tracing();
    return 0;
}
