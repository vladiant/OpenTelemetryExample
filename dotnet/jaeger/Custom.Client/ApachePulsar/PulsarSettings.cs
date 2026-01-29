namespace Custom.Client.ApachePulsar;

public sealed class PulsarSettings
{
    internal const string ConfigurationSectionName = "Aspire:Pulsar:Client";

    /// <summary>
    /// Gets or sets the endpoint address the Pulsar to connect to.
    /// </summary>
    public Uri? Endpoint { get; set; }

    /// <summary>
    /// Gets or sets a boolean value that indicates whether the Pulsar metrics are disabled or not.
    /// </summary>
    /// <value>
    /// The default value is <see langword="false"/>.
    /// </value>
    public bool DisableMetrics { get; set; }

    /// <summary>
    /// Gets or sets a boolean value that indicates whether the OpenTelemetry tracing is disabled or not.
    /// </summary>
    /// <value>
    /// The default value is <see langword="false"/>.
    /// </value>
    public bool DisableTracing { get; set; }


    internal void ParseConnectionString(string? connectionString)
    {
        // Parse the connection string and set the properties accordingly
        if (string.IsNullOrEmpty(connectionString)) {
            throw new Exception("Invalid connection string - empty or missing");
        }

        if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri)) {
            Endpoint = uri;
        } else {
            throw new Exception("Invalid connection string - not a valid URI");
        }
    }
}
