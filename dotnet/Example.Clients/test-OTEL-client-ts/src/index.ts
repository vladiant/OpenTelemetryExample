// Initialize OpenTelemetry first, before any other imports
import './telemetry';
import { SimpleTest } from './simpleTest';

async function main() {
    // Parse command line arguments
    const args = process.argv.slice(2);
    const runComplexTest = args.length > 0 && args[0].toLowerCase() === 'complex';

    if (runComplexTest) {
        console.log('Running complex test...');
        await SimpleTest.runComplex();
    } else {
        console.log('Running simple test...');
        await SimpleTest.run();
    }
}

main().catch((error) => {
    console.error('Fatal error:', error);
    process.exit(1);
});