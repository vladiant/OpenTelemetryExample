

using System.Diagnostics;
using DotPulsar.Abstractions;
using Serilog;

namespace DotnetTelemetryPlayground.ApiService;

// Extension method for creating activity from message
internal static class ActivityExtensions
{
    // Extension method to create an activity from a message
    public static Activity? StartActivity<T>(this IServiceScopeFactory serviceScopeFactory, IMessage<T> message, string activityName = "ProcessingActivity")
    {
        try {
            // Get activity source for module
            using IServiceScope scope = serviceScopeFactory.CreateScope();
            ActivitySource source = scope.ServiceProvider.GetRequiredService<ActivitySource>();

            // Extract trace context from message properties
            bool parseRes = ActivityContext.TryParse(message.Properties.GetValueOrDefault(PulsarConstants.TraceIdTAG) ?? string.Empty,
                                                     message.Properties.GetValueOrDefault(PulsarConstants.TraceStateTAG),
                                                     out ActivityContext parentContext
            );

            // Start a new activity for processing the message
            Activity? result = parseRes ?
                                    source.StartActivity(activityName, ActivityKind.Consumer, parentContext) :
                                    source.StartActivity(activityName, ActivityKind.Consumer);

            // Add relevant tags to the activity
            result?.SetTag("messaging.message_id", message.MessageId.ToString());

            // return
            return result;
        } catch (Exception err) {
            // log error
            Log.Error(err, "Error starting activity from Pulsar message. MessageId: {MessageId}", message.MessageId);

            // in case of error return null
            return null;
        }
    }
}