using DotPulsar;
using DotPulsar.Abstractions;
using DotPulsar.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace Microsoft.Extensions.Hosting
{
    public static class PulsarExtentions
    {
        public static IServiceCollection AddPulsarClient(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddSingleton<IPulsarClient>(sp =>
            {
                // Try multiple configuration key formats with extensive logging
                Console.WriteLine("[PULSAR] Starting Pulsar client configuration...");
                
                var serviceUrl = configuration["Pulsar:ServiceUrl"] 
                              ?? configuration["Pulsar__ServiceUrl"]
                              ?? Environment.GetEnvironmentVariable("Pulsar__ServiceUrl")
                              ?? Environment.GetEnvironmentVariable("Pulsar:ServiceUrl")
                              ?? "pulsar://pulsar:6650";
                
                Console.WriteLine($"[PULSAR] Configuration values found:");
                Console.WriteLine($"  - Pulsar:ServiceUrl = {configuration["Pulsar:ServiceUrl"]}");
                Console.WriteLine($"  - Pulsar__ServiceUrl = {configuration["Pulsar__ServiceUrl"]}");
                Console.WriteLine($"  - ENV Pulsar__ServiceUrl = {Environment.GetEnvironmentVariable("Pulsar__ServiceUrl")}");
                Console.WriteLine($"[PULSAR] Using ServiceUrl: {serviceUrl}");
                
                if (string.IsNullOrWhiteSpace(serviceUrl))
                {
                    var error = "Pulsar ServiceUrl is null or empty after checking all sources!";
                    Console.WriteLine($"[PULSAR] ERROR: {error}");
                    throw new InvalidOperationException(error);
                }

                try
                {
                    if (!Uri.TryCreate(serviceUrl, UriKind.Absolute, out var uri))
                    {
                        throw new InvalidOperationException($"Invalid Pulsar URL format: {serviceUrl}");
                    }

                    Console.WriteLine($"[PULSAR] Building Pulsar client with URI: {uri}");
                    
                    var client = PulsarClient.Builder()
                        .ServiceUrl(uri)
                        .Build();
                    
                    Console.WriteLine($"[PULSAR] ✅ Pulsar client created successfully!");
                    return client;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PULSAR] ❌ ERROR: {ex.Message}");
                    Console.WriteLine($"[PULSAR] Stack trace: {ex.StackTrace}");
                    throw;
                }
            });

            return services;
        }

        public static IHostApplicationBuilder AddPulsarClient(
            this IHostApplicationBuilder builder)
        {
            return AddPulsarClient(builder, builder.Configuration);
        }

        public static IHostApplicationBuilder AddPulsarClient(
            this IHostApplicationBuilder builder,
            IConfiguration configuration)
        {
            builder.Services.AddPulsarClient(configuration);
            return builder;
        }
    }
}