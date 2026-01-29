using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace DotnetTelemetryPlayground.ApiService;

// Define a Meter for the ApiService
public class ApiServiceMeter : IDisposable
{
    #region Constants and Fields

    public const string MeterName = "ApiService.Processor";

    private const string WeatherCreatedEvent = "weather_created";
    private const string WeatherUpdatedEvent = "weather_updated";

    private readonly Meter _meter;
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _windowSize = TimeSpan.FromSeconds(60); // 1-minute window
    private readonly ConcurrentQueue<DateTime> _requestCreateTimestamps;
    private readonly ConcurrentQueue<DateTime> _requestUpdateTimestamps;

    private readonly Histogram<int> _weatherCreatedProcessingTime;
    private readonly Histogram<int> _weatherUpdatedProcessingTime;

    #endregion

    public ApiServiceMeter(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);
        _requestCreateTimestamps = new ConcurrentQueue<DateTime>();
        _requestUpdateTimestamps = new ConcurrentQueue<DateTime>();

        // Clean up old timestamps every 10 seconds
        _cleanupTimer = new Timer(CleanupOldTimestamps, null,
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

        _weatherCreatedProcessingTime = _meter.CreateHistogram<int>("api_service.processor.weather_created_processing_time", "ms", "Processing time for weather created events");
        _weatherUpdatedProcessingTime = _meter.CreateHistogram<int>("api_service.processor.weather_updated_processing_time", "ms", "Processing time for weather updated events");
        _meter.CreateObservableGauge("api_service.processor.weather_created_per_second", () => CalculateRequestsPerSecond(WeatherCreatedEvent), "requests/second", "Rate of weather created events");
        _meter.CreateObservableGauge("api_service.processor.weather_updated_per_second", () => CalculateRequestsPerSecond(WeatherUpdatedEvent), "requests/second", "Rate of weather updated events");
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _meter.Dispose();
    }


    public double CalculateRequestsPerSecond(string eventType)
    {
        var cutoffTime = DateTime.UtcNow - _windowSize;
        var validRequests = 0;

        // Count valid requests based on event type
        switch (eventType) {
            case WeatherCreatedEvent:
                foreach (var timestamp in _requestCreateTimestamps) {
                    if (timestamp >= cutoffTime)
                        validRequests++;
                }
                break;
            case WeatherUpdatedEvent:
                foreach (var timestamp in _requestUpdateTimestamps) {
                    if (timestamp >= cutoffTime)
                        validRequests++;
                }
                break;
            default:
                throw new ArgumentException("Invalid event type. Must be 'created' or 'updated'.");
        }

        // return the rate per second
        return validRequests / _windowSize.TotalSeconds;
    }

    // Method to clean up old timestamps from the queues
    private void CleanupOldTimestamps(object? state)
    {
        var cutoffTime = DateTime.UtcNow - _windowSize;

        // Clean up both queues in parallel
        Task queueOne = Task.Run(() =>
        {
            while (_requestCreateTimestamps.TryPeek(out var timestamp) && timestamp < cutoffTime) {
                _requestCreateTimestamps.TryDequeue(out _);
            }
        });
        Task queueTwo = Task.Run(() =>
        {
            while (_requestUpdateTimestamps.TryPeek(out var timestamp) && timestamp < cutoffTime) {
                _requestUpdateTimestamps.TryDequeue(out _);
            }
        });

        // Wait for both queues to finish cleaning up
        Task.WaitAll(queueOne, queueTwo);
    }
    
    public void RecordWeatherCreated(int processingTimeMs)
    {
        _requestCreateTimestamps.Enqueue(DateTime.UtcNow);
        _weatherCreatedProcessingTime.Record(processingTimeMs);
    }

    public void RecordWeatherUpdated(int processingTimeMs)
    {
        _requestUpdateTimestamps.Enqueue(DateTime.UtcNow);
        _weatherUpdatedProcessingTime.Record(processingTimeMs);
    }
}
