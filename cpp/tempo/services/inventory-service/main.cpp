/**
 * Inventory Service - Manages product inventory.
 * 
 * This service demonstrates:
 * - Leaf service in the trace chain
 * - Database-like operations with spans
 * - Error handling and span status
 * - Custom span events and attributes
 */

#include <iostream>
#include <string>
#include <map>
#include <mutex>
#include <thread>
#include <chrono>

#define CPPHTTPLIB_OPENSSL_SUPPORT
#include <httplib.h>
#include <nlohmann/json.hpp>

#include "../common/tracing.hpp"

using json = nlohmann::json;

// Product structure
struct Product {
    std::string product_id;
    std::string name;
    int quantity;
    double price;
    int reserved;
};

// In-memory inventory storage
std::map<std::string, Product> inventory_db;
std::map<std::string, json> reservations;
std::mutex db_mutex;

void init_inventory() {
    inventory_db["demo-product"] = {"demo-product", "Demo Product", 100, 29.99, 0};
    inventory_db["laptop-001"] = {"laptop-001", "Business Laptop", 50, 999.99, 0};
    inventory_db["phone-001"] = {"phone-001", "Smartphone Pro", 200, 699.99, 0};
    inventory_db["headphones-001"] = {"headphones-001", "Wireless Headphones", 75, 149.99, 0};
}

json product_to_json(const Product& p) {
    return {
        {"product_id", p.product_id},
        {"name", p.name},
        {"quantity", p.quantity},
        {"price", p.price},
        {"reserved", p.reserved}
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
    std::string service_name = "inventory-service";
    if (const char* env = std::getenv("SERVICE_NAME")) {
        service_name = env;
    }
    
    std::string otlp_endpoint = "localhost:4317";
    if (const char* env = std::getenv("OTEL_EXPORTER_OTLP_ENDPOINT")) {
        otlp_endpoint = env;
    }
    
    // Initialize tracing
    tracing::init_tracing(service_name, otlp_endpoint);
    auto tracer = tracing::get_tracer(service_name);
    
    // Initialize inventory
    init_inventory();
    
    std::cout << "Tracing configured for " << service_name << " -> " << otlp_endpoint << std::endl;
    
    httplib::Server svr;
    
    // Health check
    svr.Get("/", [](const httplib::Request&, httplib::Response& res) {
        json response = {{"service", "inventory-service"}, {"status", "healthy"}};
        res.set_content(response.dump(), "application/json");
    });
    
    svr.Get("/health", [](const httplib::Request&, httplib::Response& res) {
        std::lock_guard<std::mutex> lock(db_mutex);
        int total_items = 0;
        for (const auto& [_, p] : inventory_db) {
            total_items += p.quantity;
        }
        json response = {
            {"service", "inventory-service"},
            {"status", "healthy"},
            {"products_count", inventory_db.size()},
            {"total_items", total_items}
        };
        res.set_content(response.dump(), "application/json");
    });
    
    // List all inventory
    svr.Get("/inventory", [&tracer](const httplib::Request& req, httplib::Response& res) {
        auto headers = get_headers(req);
        auto ctx = tracing::extract_context(headers);
        auto scope = opentelemetry::context::RuntimeContext::Attach(ctx);
        
        auto span = tracer->StartSpan("list_inventory");
        auto span_scope = tracer->WithActiveSpan(span);
        
        span->SetAttribute("db.system", "in-memory");
        span->SetAttribute("db.operation", "select");
        span->SetAttribute("db.table", "inventory");
        
        // Simulate database query
        std::this_thread::sleep_for(std::chrono::milliseconds(20));
        
        std::lock_guard<std::mutex> lock(db_mutex);
        json items = json::array();
        for (const auto& [_, p] : inventory_db) {
            items.push_back(product_to_json(p));
        }
        
        span->SetAttribute("result.count", static_cast<int64_t>(items.size()));
        span->AddEvent("Inventory query completed", {{"items_returned", static_cast<int64_t>(items.size())}});
        
        span->End();
        res.set_content(items.dump(), "application/json");
    });
    
    // Get inventory for specific product
    svr.Get(R"(/inventory/([^/]+))", [&tracer](const httplib::Request& req, httplib::Response& res) {
        auto headers = get_headers(req);
        auto ctx = tracing::extract_context(headers);
        auto scope = opentelemetry::context::RuntimeContext::Attach(ctx);
        
        std::string product_id = req.matches[1];
        
        auto span = tracer->StartSpan("get_inventory_item");
        auto span_scope = tracer->WithActiveSpan(span);
        
        span->SetAttribute("db.system", "in-memory");
        span->SetAttribute("db.operation", "select");
        span->SetAttribute("db.table", "inventory");
        span->SetAttribute("product.id", product_id);
        
        // Simulate database lookup
        std::this_thread::sleep_for(std::chrono::milliseconds(10));
        
        std::lock_guard<std::mutex> lock(db_mutex);
        
        if (inventory_db.find(product_id) == inventory_db.end()) {
            span->SetStatus(opentelemetry::trace::StatusCode::kError, "Product not found");
            span->AddEvent("Product lookup failed", {{"product_id", product_id}, {"reason", "not_found"}});
            span->End();
            
            res.status = 404;
            res.set_content(R"({"detail": "Product not found"})", "application/json");
            return;
        }
        
        const auto& item = inventory_db[product_id];
        int available = item.quantity - item.reserved;
        
        span->SetAttribute("inventory.quantity", static_cast<int64_t>(item.quantity));
        span->SetAttribute("inventory.reserved", static_cast<int64_t>(item.reserved));
        span->SetAttribute("inventory.available", static_cast<int64_t>(available));
        span->AddEvent("Product found", {{"product_id", product_id}, {"available", static_cast<int64_t>(available)}});
        
        json response = product_to_json(item);
        response["available"] = available;
        
        span->End();
        res.set_content(response.dump(), "application/json");
    });
    
    // Reserve inventory
    svr.Post(R"(/inventory/([^/]+)/reserve)", [&tracer](const httplib::Request& req, httplib::Response& res) {
        auto headers = get_headers(req);
        auto ctx = tracing::extract_context(headers);
        auto scope = opentelemetry::context::RuntimeContext::Attach(ctx);
        
        std::string product_id = req.matches[1];
        json body = json::parse(req.body);
        int quantity = body.value("quantity", 0);
        std::string order_id = body.value("order_id", "unknown");
        
        auto span = tracer->StartSpan("reserve_inventory");
        auto span_scope = tracer->WithActiveSpan(span);
        
        span->SetAttribute("db.system", "in-memory");
        span->SetAttribute("db.operation", "update");
        span->SetAttribute("db.table", "inventory");
        span->SetAttribute("product.id", product_id);
        span->SetAttribute("reservation.quantity", static_cast<int64_t>(quantity));
        span->SetAttribute("reservation.order_id", order_id);
        
        std::cout << "Reserving " << quantity << " units of " << product_id << " for order " << order_id << std::endl;
        
        std::lock_guard<std::mutex> lock(db_mutex);
        
        // Check product exists
        if (inventory_db.find(product_id) == inventory_db.end()) {
            span->SetStatus(opentelemetry::trace::StatusCode::kError, "Product not found");
            span->End();
            res.status = 404;
            res.set_content(R"({"detail": "Product not found"})", "application/json");
            return;
        }
        
        auto& item = inventory_db[product_id];
        int available = item.quantity - item.reserved;
        
        // Check availability
        {
            auto check_span = tracer->StartSpan("check_availability");
            check_span->SetAttribute("inventory.available", static_cast<int64_t>(available));
            check_span->SetAttribute("inventory.requested", static_cast<int64_t>(quantity));
            
            if (available < quantity) {
                check_span->SetStatus(opentelemetry::trace::StatusCode::kError, "Insufficient inventory");
                check_span->AddEvent("Reservation failed", {
                    {"reason", "insufficient_inventory"},
                    {"available", static_cast<int64_t>(available)},
                    {"requested", static_cast<int64_t>(quantity)}
                });
                check_span->End();
                span->SetStatus(opentelemetry::trace::StatusCode::kError, "Insufficient inventory");
                span->End();
                
                res.status = 400;
                json error = {{"detail", "Insufficient inventory. Available: " + std::to_string(available) + ", Requested: " + std::to_string(quantity)}};
                res.set_content(error.dump(), "application/json");
                return;
            }
            
            check_span->AddEvent("Availability confirmed");
            check_span->End();
        }
        
        // Perform reservation
        {
            auto update_span = tracer->StartSpan("update_reservation");
            update_span->SetAttribute("db.operation", "update");
            
            // Simulate database transaction
            std::this_thread::sleep_for(std::chrono::milliseconds(30));
            
            item.reserved += quantity;
            reservations[order_id] = {
                {"order_id", order_id},
                {"product_id", product_id},
                {"quantity", quantity},
                {"status", "reserved"}
            };
            
            update_span->AddEvent("Reservation committed", {{"new_reserved", static_cast<int64_t>(item.reserved)}});
            update_span->End();
        }
        
        span->AddEvent("Reservation completed successfully");
        span->End();
        
        std::cout << "Reserved " << quantity << " units of " << product_id << " for order " << order_id << std::endl;
        
        json response = {
            {"status", "reserved"},
            {"product_id", product_id},
            {"quantity", quantity},
            {"order_id", order_id},
            {"remaining_available", item.quantity - item.reserved}
        };
        res.set_content(response.dump(), "application/json");
    });
    
    // Release inventory
    svr.Post(R"(/inventory/([^/]+)/release)", [&tracer](const httplib::Request& req, httplib::Response& res) {
        auto headers = get_headers(req);
        auto ctx = tracing::extract_context(headers);
        auto scope = opentelemetry::context::RuntimeContext::Attach(ctx);
        
        std::string product_id = req.matches[1];
        json body = json::parse(req.body);
        std::string order_id = body.value("order_id", "");
        
        auto span = tracer->StartSpan("release_inventory");
        auto span_scope = tracer->WithActiveSpan(span);
        
        span->SetAttribute("product.id", product_id);
        span->SetAttribute("order.id", order_id);
        
        std::lock_guard<std::mutex> lock(db_mutex);
        
        if (reservations.find(order_id) == reservations.end()) {
            span->SetStatus(opentelemetry::trace::StatusCode::kError, "Reservation not found");
            span->End();
            res.status = 404;
            res.set_content(R"({"detail": "Reservation not found"})", "application/json");
            return;
        }
        
        int quantity = reservations[order_id]["quantity"];
        
        // Simulate database update
        std::this_thread::sleep_for(std::chrono::milliseconds(20));
        
        inventory_db[product_id].reserved -= quantity;
        reservations.erase(order_id);
        
        span->SetAttribute("released.quantity", static_cast<int64_t>(quantity));
        span->AddEvent("Inventory released", {{"quantity", static_cast<int64_t>(quantity)}, {"order_id", order_id}});
        span->End();
        
        json response = {
            {"status", "released"},
            {"product_id", product_id},
            {"quantity", quantity},
            {"order_id", order_id}
        };
        res.set_content(response.dump(), "application/json");
    });
    
    // Add inventory
    svr.Post(R"(/inventory/([^/]+)/add)", [&tracer](const httplib::Request& req, httplib::Response& res) {
        auto headers = get_headers(req);
        auto ctx = tracing::extract_context(headers);
        auto scope = opentelemetry::context::RuntimeContext::Attach(ctx);
        
        std::string product_id = req.matches[1];
        json body = json::parse(req.body);
        int quantity = body.value("quantity", 0);
        
        auto span = tracer->StartSpan("add_inventory");
        auto span_scope = tracer->WithActiveSpan(span);
        
        span->SetAttribute("product.id", product_id);
        span->SetAttribute("quantity.added", static_cast<int64_t>(quantity));
        
        std::lock_guard<std::mutex> lock(db_mutex);
        
        if (inventory_db.find(product_id) == inventory_db.end()) {
            span->SetStatus(opentelemetry::trace::StatusCode::kError, "Product not found");
            span->End();
            res.status = 404;
            res.set_content(R"({"detail": "Product not found"})", "application/json");
            return;
        }
        
        // Simulate database update
        std::this_thread::sleep_for(std::chrono::milliseconds(20));
        
        inventory_db[product_id].quantity += quantity;
        int new_quantity = inventory_db[product_id].quantity;
        
        span->SetAttribute("quantity.new_total", static_cast<int64_t>(new_quantity));
        span->AddEvent("Inventory added", {{"added", static_cast<int64_t>(quantity)}, {"new_total", static_cast<int64_t>(new_quantity)}});
        span->End();
        
        json response = {
            {"product_id", product_id},
            {"quantity_added", quantity},
            {"new_total", new_quantity}
        };
        res.set_content(response.dump(), "application/json");
    });
    
    std::cout << "Inventory Service starting on port 8002..." << std::endl;
    svr.listen("0.0.0.0", 8002);
    
    tracing::cleanup_tracing();
    return 0;
}
