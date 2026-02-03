#pragma once

#include <string>
#include <memory>

#include <opentelemetry/exporters/otlp/otlp_http_exporter_factory.h>
#include <opentelemetry/exporters/otlp/otlp_http_exporter_options.h>
#include <opentelemetry/sdk/trace/processor.h>
#include <opentelemetry/sdk/trace/simple_processor_factory.h>
#include <opentelemetry/sdk/trace/tracer_provider_factory.h>
#include <opentelemetry/trace/provider.h>
#include <opentelemetry/sdk/resource/resource.h>
#include <opentelemetry/sdk/resource/semantic_conventions.h>
#include <opentelemetry/context/propagation/global_propagator.h>
#include <opentelemetry/trace/propagation/http_trace_context.h>

namespace tracing {

namespace trace_api = opentelemetry::trace;
namespace trace_sdk = opentelemetry::sdk::trace;
namespace otlp = opentelemetry::exporter::otlp;
namespace resource = opentelemetry::sdk::resource;

/**
 * Initialize OpenTelemetry tracing with OTLP HTTP exporter to Tempo.
 * 
 * @param service_name Name of the service for trace identification
 * @param otlp_endpoint Tempo OTLP HTTP endpoint (e.g., "http://tempo:4318/v1/traces")
 */
inline void init_tracing(const std::string& service_name, const std::string& otlp_endpoint) {
    // Create OTLP HTTP exporter options
    otlp::OtlpHttpExporterOptions opts;
    opts.url = otlp_endpoint;
    
    // Create OTLP HTTP exporter
    auto exporter = otlp::OtlpHttpExporterFactory::Create(opts);
    
    // Create simple span processor (simpler than batch, works reliably)
    auto processor = trace_sdk::SimpleSpanProcessorFactory::Create(std::move(exporter));
    
    // Create resource with service name
    auto resource_attrs = resource::Resource::Create({
        {resource::SemanticConventions::kServiceName, service_name},
        {resource::SemanticConventions::kServiceVersion, "1.0.0"},
        {"deployment.environment", "development"}
    });
    
    // Create tracer provider
    std::shared_ptr<trace_api::TracerProvider> provider = 
        trace_sdk::TracerProviderFactory::Create(std::move(processor), resource_attrs);
    
    // Set global tracer provider
    trace_api::Provider::SetTracerProvider(provider);
    
    // Set up W3C Trace Context propagator
    opentelemetry::context::propagation::GlobalTextMapPropagator::SetGlobalPropagator(
        opentelemetry::nostd::shared_ptr<opentelemetry::context::propagation::TextMapPropagator>(
            new opentelemetry::trace::propagation::HttpTraceContext()));
}

/**
 * Get a tracer instance for the given name.
 */
inline opentelemetry::nostd::shared_ptr<trace_api::Tracer> get_tracer(const std::string& name) {
    auto provider = trace_api::Provider::GetTracerProvider();
    return provider->GetTracer(name, "1.0.0");
}

/**
 * Cleanup tracing resources.
 */
inline void cleanup_tracing() {
    std::shared_ptr<trace_api::TracerProvider> none;
    trace_api::Provider::SetTracerProvider(none);
}

/**
 * HTTP header carrier for context propagation.
 */
class HttpTextMapCarrier : public opentelemetry::context::propagation::TextMapCarrier {
public:
    HttpTextMapCarrier(std::map<std::string, std::string>& headers) : headers_(headers) {}
    
    opentelemetry::nostd::string_view Get(opentelemetry::nostd::string_view key) const noexcept override {
        auto it = headers_.find(std::string(key));
        if (it != headers_.end()) {
            return it->second;
        }
        return "";
    }
    
    void Set(opentelemetry::nostd::string_view key, opentelemetry::nostd::string_view value) noexcept override {
        headers_[std::string(key)] = std::string(value);
    }
    
private:
    std::map<std::string, std::string>& headers_;
};

/**
 * Inject trace context into HTTP headers for outgoing requests.
 */
inline void inject_context(std::map<std::string, std::string>& headers) {
    auto propagator = opentelemetry::context::propagation::GlobalTextMapPropagator::GetGlobalPropagator();
    auto current_ctx = opentelemetry::context::RuntimeContext::GetCurrent();
    HttpTextMapCarrier carrier(headers);
    propagator->Inject(carrier, current_ctx);
}

/**
 * Extract trace context from HTTP headers for incoming requests.
 */
inline opentelemetry::context::Context extract_context(std::map<std::string, std::string>& headers) {
    auto propagator = opentelemetry::context::propagation::GlobalTextMapPropagator::GetGlobalPropagator();
    HttpTextMapCarrier carrier(headers);
    auto current_ctx = opentelemetry::context::RuntimeContext::GetCurrent();
    return propagator->Extract(carrier, current_ctx);
}

} // namespace tracing
