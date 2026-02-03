/**
 * Order Service - Handles order creation and management.
 * 
 * This service demonstrates:
 * - Receiving propagated trace context
 * - Creating child spans
 * - Calling downstream services (Inventory)
 * - Adding custom attributes and events to spans
 */

#include <iostream>
#include <string>
#include <map>
#include <mutex>
#include <thread>
#include <chrono>
#include <random>
#include <sstream>
#include <iomanip>

#define CPPHTTPLIB_OPENSSL_SUPPORT
#include <httplib.h>
#include <nlohmann/json.hpp>

#include "../common/tracing.hpp"
#include "../common/http_client.hpp"

using json = nlohmann::json;

// Order structure
struct Order {
    std::string order_id;
    std::string product_id;
    int quantity;
    std::string status;
    std::string created_at;
};

// In-memory order storage
std::map<std::string, Order> orders_db;
std::mutex db_mutex;

// Generate UUID-like string
std::string generate_uuid() {
    static std::random_device rd;
    static std::mt19937 gen(rd());
    static std::uniform_int_distribution<> dis(0, 15);
    static const char* hex = "0123456789abcdef";
    
    std::stringstream ss;
    for (int i = 0; i < 32; ++i) {
        if (i == 8 || i == 12 || i == 16 || i == 20) ss << '-';
        ss << hex[dis(gen)];
    }
    return ss.str();
}

// Get current timestamp
std::string get_timestamp() {
    auto now = std::chrono::system_clock::now();
    auto time = std::chrono::system_clock::to_time_t(now);
    std::stringstream ss;
    ss << std::put_time(std::gmtime(&time), "%Y-%m-%dT%H:%M:%SZ");
    return ss.str();
}

json order_to_json(const Order& o) {
    return {
        {"order_id", o.order_id},
        {"product_id", o.product_id},
        {"quantity", o.quantity},
        {"status", o.status},
        {"created_at", o.created_at}
    };
}

// Extract trace context from request headers
std::map<std::string, std::string> get_headers(const httplib::Request& req) {
    std::map<std::string, std::string> headers;
    for (const auto& [key, value] : req.headers) {
        headers[key] = value;
    }
    return headers;
}

int main() {
    std::string service_name = "order-service";
    if (const char* env = std::getenv("SERVICE_NAME")) {
        service_name = env;
    }
    
    std::string otlp_endpoint = "localhost:4317";
    if (const char* env = std::getenv("OTEL_EXPORTER_OTLP_ENDPOINT")) {
        otlp_endpoint = env;
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
        json response = {{"service", "order-service"}, {"status", "healthy"}};
        res.set_content(response.dump(), "application/json");
    });
    
    svr.Get("/health", [](const httplib::Request&, httplib::Response& res) {
        std::lock_guard<std::mutex> lock(db_mutex);
        json response = {
            {"service", "order-service"},
            {"status", "healthy"},
            {"orders_count", orders_db.size()}
        };
        res.set_content(response.dump(), "application/json");
    });
    
    // List all orders
    svr.Get("/orders", [&tracer](const httplib::Request& req, httplib::Response& res) {
        auto headers = get_headers(req);
        auto ctx = tracing::extract_context(headers);
        auto scope = opentelemetry::context::RuntimeContext::Attach(ctx);
        
        auto span = tracer->StartSpan("list_orders");
        auto span_scope = tracer->WithActiveSpan(span);
        
        std::lock_guard<std::mutex> lock(db_mutex);
        json orders = json::array();
        for (const auto& [_, o] : orders_db) {
            orders.push_back(order_to_json(o));
        }
        
        span->SetAttribute("orders.count", static_cast<int64_t>(orders.size()));
        span->End();
        
        res.set_content(orders.dump(), "application/json");
    });
    
    // Get order by ID
    svr.Get(R"(/orders/([^/]+))", [&tracer](const httplib::Request& req, httplib::Response& res) {
        auto headers = get_headers(req);
        auto ctx = tracing::extract_context(headers);
        auto scope = opentelemetry::context::RuntimeContext::Attach(ctx);
        
        std::string order_id = req.matches[1];
        
        auto span = tracer->StartSpan("get_order");
        auto span_scope = tracer->WithActiveSpan(span);
        
        span->SetAttribute("order.id", order_id);
        
        // Simulate database read
        std::this_thread::sleep_for(std::chrono::milliseconds(10));
        
        std::lock_guard<std::mutex> lock(db_mutex);
        
        if (orders_db.find(order_id) == orders_db.end()) {
            span->SetStatus(opentelemetry::trace::StatusCode::kError, "Order not found");
            span->End();
            res.status = 404;
            res.set_content(R"({"detail": "Order not found"})", "application/json");
            return;
        }
        
        const auto& order = orders_db[order_id];
        span->SetAttribute("order.status", order.status);
        span->End();
        
        res.set_content(order_to_json(order).dump(), "application/json");
    });
    
    // Create order
    svr.Post("/orders", [&tracer, &inventory_host, &inventory_port](const httplib::Request& req, httplib::Response& res) {
        auto headers = get_headers(req);
        auto ctx = tracing::extract_context(headers);
        auto scope = opentelemetry::context::RuntimeContext::Attach(ctx);
        
        json body;
        try {
            body = json::parse(req.body);
        } catch (...) {
            body = {{"product_id", "demo-product"}, {"quantity", 1}};
        }
        
        std::string product_id = body.value("product_id", "demo-product");
        int quantity = body.value("quantity", 1);
        
        auto span = tracer->StartSpan("create_order");
        auto span_scope = tracer->WithActiveSpan(span);
        
        std::string order_id = generate_uuid();
        span->SetAttribute("order.id", order_id);
        span->SetAttribute("order.product_id", product_id);
        span->SetAttribute("order.quantity", static_cast<int64_t>(quantity));
        
        span->AddEvent("Order processing started", {
            {"order.id", order_id},
            {"timestamp", get_timestamp()}
        });
        
        std::cout << "Creating order " << order_id << " for product " << product_id << std::endl;
        
        // Step 1: Validate order
        {
            auto validate_span = tracer->StartSpan("validate_order");
            validate_span->SetAttribute("validation.product_id", product_id);
            validate_span->SetAttribute("validation.quantity", static_cast<int64_t>(quantity));
            
            if (quantity <= 0) {
                validate_span->SetStatus(opentelemetry::trace::StatusCode::kError, "Invalid quantity");
                validate_span->End();
                span->SetStatus(opentelemetry::trace::StatusCode::kError, "Validation failed");
                span->End();
                
                res.status = 400;
                res.set_content(R"({"detail": "Quantity must be positive"})", "application/json");
                return;
            }
            
            // Simulate validation delay
            std::this_thread::sleep_for(std::chrono::milliseconds(50));
            validate_span->AddEvent("Validation passed");
            validate_span->End();
        }
        
        http_client::TracedHttpClient inventory_client(inventory_host, inventory_port);
        
        // Step 2: Check inventory
        {
            auto inv_span = tracer->StartSpan("check_inventory");
            inv_span->SetAttribute("inventory.product_id", product_id);
            inv_span->SetAttribute("inventory.requested_quantity", static_cast<int64_t>(quantity));
            
            auto inv_response = inventory_client.get("/inventory/" + product_id, "HTTP GET inventory");
            
            if (inv_response.status == 404) {
                inv_span->SetStatus(opentelemetry::trace::StatusCode::kError, "Product not found");
                inv_span->End();
                span->SetStatus(opentelemetry::trace::StatusCode::kError, "Product not found");
                span->End();
                
                res.status = 404;
                res.set_content(R"({"detail": "Product not found"})", "application/json");
                return;
            }
            
            if (!inv_response.success) {
                inv_span->SetStatus(opentelemetry::trace::StatusCode::kError, "Inventory service unavailable");
                inv_span->End();
                span->SetStatus(opentelemetry::trace::StatusCode::kError, "Inventory service unavailable");
                span->End();
                
                res.status = 503;
                res.set_content(R"({"detail": "Inventory service unavailable"})", "application/json");
                return;
            }
            
            int available = inv_response.json_body.value("quantity", 0);
            inv_span->SetAttribute("inventory.available", static_cast<int64_t>(available));
            
            if (available < quantity) {
                inv_span->SetStatus(opentelemetry::trace::StatusCode::kError, "Insufficient inventory");
                inv_span->End();
                span->SetStatus(opentelemetry::trace::StatusCode::kError, "Insufficient inventory");
                span->End();
                
                res.status = 400;
                json error = {{"detail", "Insufficient inventory. Available: " + std::to_string(available)}};
                res.set_content(error.dump(), "application/json");
                return;
            }
            
            inv_span->AddEvent("Inventory check passed", {
                {"available", static_cast<int64_t>(available)},
                {"requested", static_cast<int64_t>(quantity)}
            });
            inv_span->End();
        }
        
        // Step 3: Reserve inventory
        {
            auto reserve_span = tracer->StartSpan("reserve_inventory");
            reserve_span->SetAttribute("reservation.product_id", product_id);
            reserve_span->SetAttribute("reservation.quantity", static_cast<int64_t>(quantity));
            
            json reserve_body = {{"quantity", quantity}, {"order_id", order_id}};
            auto reserve_response = inventory_client.post("/inventory/" + product_id + "/reserve", reserve_body, "HTTP POST reserve");
            
            if (!reserve_response.success) {
                reserve_span->SetStatus(opentelemetry::trace::StatusCode::kError, "Reservation failed");
                reserve_span->End();
                span->SetStatus(opentelemetry::trace::StatusCode::kError, "Reservation failed");
                span->End();
                
                res.status = reserve_response.status > 0 ? reserve_response.status : 503;
                res.set_content(R"({"detail": "Failed to reserve inventory"})", "application/json");
                return;
            }
            
            reserve_span->AddEvent("Inventory reserved successfully");
            reserve_span->End();
        }
        
        // Step 4: Create order record
        {
            auto persist_span = tracer->StartSpan("persist_order");
            
            Order order;
            order.order_id = order_id;
            order.product_id = product_id;
            order.quantity = quantity;
            order.status = "confirmed";
            order.created_at = get_timestamp();
            
            // Simulate database write
            std::this_thread::sleep_for(std::chrono::milliseconds(20));
            
            {
                std::lock_guard<std::mutex> lock(db_mutex);
                orders_db[order_id] = order;
            }
            
            persist_span->SetAttribute("db.operation", "insert");
            persist_span->SetAttribute("db.table", "orders");
            persist_span->AddEvent("Order persisted to database");
            persist_span->End();
        }
        
        span->AddEvent("Order processing completed", {
            {"order.id", order_id},
            {"order.status", "confirmed"}
        });
        span->End();
        
        std::cout << "Order " << order_id << " created successfully" << std::endl;
        
        std::lock_guard<std::mutex> lock(db_mutex);
        res.set_content(order_to_json(orders_db[order_id]).dump(), "application/json");
    });
    
    std::cout << "Order Service starting on port 8001..." << std::endl;
    svr.listen("0.0.0.0", 8001);
    
    tracing::cleanup_tracing();
    return 0;
}
