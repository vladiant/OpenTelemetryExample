import { NodeSDK } from '@opentelemetry/sdk-node';
import { resourceFromAttributes } from '@opentelemetry/resources';
import { ATTR_SERVICE_NAME, ATTR_SERVICE_VERSION } from '@opentelemetry/semantic-conventions';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-grpc';
import { DefaultValues } from './defaultValues';

// Configure the OpenTelemetry SDK
const sdk = new NodeSDK({
    resource: resourceFromAttributes({
        [ATTR_SERVICE_NAME]: DefaultValues.SERVICE_NAME,
        [ATTR_SERVICE_VERSION]: '1.0',
    }),
    traceExporter: new OTLPTraceExporter({
        url: DefaultValues.ENDPOINT,
        headers: {
            [DefaultValues.API_KEY.split('=')[0]]: DefaultValues.API_KEY.split('=')[1]
        }
    })
});

// Initialize the SDK
sdk.start();

// Keep the process running
setInterval(() => {
  // Do nothing, just keep the event loop alive
}, 1000000);

console.log('OpenTelemetry SDK initialized successfully. ');

// Handle graceful shutdown
process.on('SIGINT', () => {
    // Ensure all spans are flushed before exit
    sdk.shutdown()
        .then(() => console.log('OpenTelemetry terminated'))
        .catch((error) => console.log('Error terminating OpenTelemetry', error))
        .finally(() => process.exit(0));
});