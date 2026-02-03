#pragma once

#include <string>
#include <map>
#include <functional>
#include <httplib.h>
#include <nlohmann/json.hpp>
#include "tracing.hpp"

namespace http_client {

using json = nlohmann::json;

struct Response {
    int status;
    std::string body;
    json json_body;
    bool success;
    std::string error;
};

/**
 * Simple HTTP client with tracing support.
 */
class TracedHttpClient {
public:
    TracedHttpClient(const std::string& host, int port) 
        : host_(host), port_(port), client_(host, port) {
        client_.set_connection_timeout(30);
        client_.set_read_timeout(30);
    }
    
    Response get(const std::string& path, const std::string& span_name = "") {
        auto tracer = tracing::get_tracer("http-client");
        auto span = tracer->StartSpan(span_name.empty() ? "HTTP GET " + path : span_name);
        auto scope = tracer->WithActiveSpan(span);
        
        span->SetAttribute("http.method", "GET");
        span->SetAttribute("http.url", "http://" + host_ + ":" + std::to_string(port_) + path);
        span->SetAttribute("http.host", host_);
        span->SetAttribute("http.port", port_);
        
        // Inject trace context into headers
        std::map<std::string, std::string> headers;
        tracing::inject_context(headers);
        
        httplib::Headers http_headers;
        for (const auto& [key, value] : headers) {
            http_headers.insert({key, value});
        }
        
        Response response;
        auto result = client_.Get(path, http_headers);
        
        if (result) {
            response.status = result->status;
            response.body = result->body;
            response.success = (result->status >= 200 && result->status < 300);
            
            span->SetAttribute("http.status_code", result->status);
            
            try {
                response.json_body = json::parse(result->body);
            } catch (...) {
                // Not JSON, that's okay
            }
            
            if (!response.success) {
                span->SetStatus(opentelemetry::trace::StatusCode::kError, "HTTP error");
            }
        } else {
            response.success = false;
            response.error = "Connection failed";
            response.status = 0;
            span->SetStatus(opentelemetry::trace::StatusCode::kError, "Connection failed");
        }
        
        span->End();
        return response;
    }
    
    Response post(const std::string& path, const json& body, const std::string& span_name = "") {
        auto tracer = tracing::get_tracer("http-client");
        auto span = tracer->StartSpan(span_name.empty() ? "HTTP POST " + path : span_name);
        auto scope = tracer->WithActiveSpan(span);
        
        span->SetAttribute("http.method", "POST");
        span->SetAttribute("http.url", "http://" + host_ + ":" + std::to_string(port_) + path);
        span->SetAttribute("http.host", host_);
        span->SetAttribute("http.port", port_);
        
        // Inject trace context into headers
        std::map<std::string, std::string> headers;
        tracing::inject_context(headers);
        
        httplib::Headers http_headers;
        for (const auto& [key, value] : headers) {
            http_headers.insert({key, value});
        }
        
        Response response;
        auto result = client_.Post(path, http_headers, body.dump(), "application/json");
        
        if (result) {
            response.status = result->status;
            response.body = result->body;
            response.success = (result->status >= 200 && result->status < 300);
            
            span->SetAttribute("http.status_code", result->status);
            
            try {
                response.json_body = json::parse(result->body);
            } catch (...) {
                // Not JSON, that's okay
            }
            
            if (!response.success) {
                span->SetStatus(opentelemetry::trace::StatusCode::kError, "HTTP error");
            }
        } else {
            response.success = false;
            response.error = "Connection failed";
            response.status = 0;
            span->SetStatus(opentelemetry::trace::StatusCode::kError, "Connection failed");
        }
        
        span->End();
        return response;
    }
    
private:
    std::string host_;
    int port_;
    httplib::Client client_;
};

} // namespace http_client
