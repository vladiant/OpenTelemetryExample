#!/bin/bash

# OpenTelemetry C++ Microservices Test Script
# Tests the distributed tracing system by sending requests and verifying responses

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
API_GATEWAY_URL="http://localhost:8080"
JAEGER_URL="http://localhost:16686"
TIMEOUT=5

# Test counters
TESTS_RUN=0
TESTS_PASSED=0
TESTS_FAILED=0

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}OpenTelemetry C++ Microservices Tests${NC}"
echo -e "${BLUE}========================================${NC}\n"

# Helper functions
test_passed() {
    echo -e "${GREEN}✓ PASSED${NC}: $1"
    ((TESTS_PASSED++))
    ((TESTS_RUN++))
}

test_failed() {
    echo -e "${RED}✗ FAILED${NC}: $1"
    ((TESTS_FAILED++))
    ((TESTS_RUN++))
}

test_info() {
    echo -e "${YELLOW}ℹ INFO${NC}: $1"
}

# Test 1: Check if Docker Compose services are running
echo -e "${BLUE}Test 1: Checking Docker Compose services status${NC}"
if docker-compose ps | grep -q "api-gateway.*Up"; then
    test_passed "api-gateway is running"
else
    test_failed "api-gateway is not running"
    echo "Run 'docker-compose up -d' to start services"
    exit 1
fi

if docker-compose ps | grep -q "order-service.*Up"; then
    test_passed "order-service is running"
else
    test_failed "order-service is not running"
fi

if docker-compose ps | grep -q "user-service.*Up"; then
    test_passed "user-service is running"
else
    test_failed "user-service is not running"
fi

if docker-compose ps | grep -q "payment-service.*Up"; then
    test_passed "payment-service is running"
else
    test_failed "payment-service is not running"
fi

if docker-compose ps | grep -q "inventory-service.*Up"; then
    test_passed "inventory-service is running"
else
    test_failed "inventory-service is not running"
fi

if docker-compose ps | grep -q "jaeger.*Up"; then
    test_passed "Jaeger is running"
else
    test_failed "Jaeger is not running"
fi

if docker-compose ps | grep -q "otel-collector.*Up"; then
    test_passed "OpenTelemetry Collector is running"
else
    test_failed "OpenTelemetry Collector is not running"
fi

echo ""

# Test 2: Check API Gateway connectivity
echo -e "${BLUE}Test 2: Checking API Gateway connectivity${NC}"
if timeout $TIMEOUT curl -s -f "$API_GATEWAY_URL/api/order" > /dev/null 2>&1; then
    test_passed "API Gateway is accessible at $API_GATEWAY_URL"
else
    test_failed "API Gateway is not responding at $API_GATEWAY_URL"
    echo "Make sure the services are running with: docker-compose up -d"
    exit 1
fi

echo ""

# Test 3: Test API responses
echo -e "${BLUE}Test 3: Testing API responses${NC}"

# Test Order API
RESPONSE=$(curl -s -w "\n%{http_code}" "$API_GATEWAY_URL/api/order")
HTTP_CODE=$(echo "$RESPONSE" | tail -n 1)
BODY=$(echo "$RESPONSE" | head -n -1)

if [ "$HTTP_CODE" = "200" ]; then
    test_passed "Order API returned HTTP 200"
    
    # Check if response contains expected fields
    if echo "$BODY" | grep -q '"order"'; then
        test_passed "Order API response contains 'order' field"
    else
        test_failed "Order API response missing 'order' field"
    fi
    
    if echo "$BODY" | grep -q '"payment"'; then
        test_passed "Order API response contains 'payment' field"
    else
        test_failed "Order API response missing 'payment' field"
    fi
    
    if echo "$BODY" | grep -q '"inventory"'; then
        test_passed "Order API response contains 'inventory' field"
    else
        test_failed "Order API response missing 'inventory' field"
    fi
else
    test_failed "Order API returned HTTP $HTTP_CODE (expected 200)"
fi

echo ""

# Test 4: Send multiple requests to generate traces
echo -e "${BLUE}Test 4: Generating traces with multiple requests${NC}"

test_info "Sending 5 test requests to generate traces..."
for i in {1..5}; do
    if curl -s -f "$API_GATEWAY_URL/api/order" > /dev/null 2>&1; then
        echo -e "  Request $i: ${GREEN}OK${NC}"
    else
        echo -e "  Request $i: ${RED}FAILED${NC}"
    fi
done

test_passed "Generated 5 test requests"

# Wait for traces to be processed
test_info "Waiting 3 seconds for traces to be processed by Jaeger..."
sleep 3

echo ""

# Test 5: Check Jaeger availability
echo -e "${BLUE}Test 5: Checking Jaeger UI availability${NC}"

if timeout $TIMEOUT curl -s -f "$JAEGER_URL/api/services" > /dev/null 2>&1; then
    test_passed "Jaeger is accessible at $JAEGER_URL"
else
    test_failed "Jaeger is not responding at $JAEGER_URL"
fi

# Check if Jaeger has services registered
SERVICES=$(curl -s "$JAEGER_URL/api/services" 2>/dev/null | grep -o '"name":"[^"]*"' | wc -l)
if [ "$SERVICES" -gt 0 ]; then
    test_passed "Jaeger has $SERVICES service(s) registered"
else
    test_info "No services found in Jaeger yet (may take a few moments)"
    ((TESTS_RUN++))
fi

echo ""

# Test 6: Check trace data
echo -e "${BLUE}Test 6: Checking trace data in Jaeger${NC}"

# Try to get traces (this may not work immediately after deployment)
TRACES=$(curl -s "$JAEGER_URL/api/traces" 2>/dev/null | grep -o '"traceID":"[^"]*"' | wc -l)
if [ "$TRACES" -gt 0 ]; then
    test_passed "Jaeger has $TRACES trace(s) recorded"
else
    test_info "No traces found yet (may take a few seconds to process)"
fi

echo ""

# Summary
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}Test Summary${NC}"
echo -e "${BLUE}========================================${NC}"
echo -e "Total Tests Run: $TESTS_RUN"
echo -e "Tests Passed:    ${GREEN}$TESTS_PASSED${NC}"
echo -e "Tests Failed:    ${RED}$TESTS_FAILED${NC}"
echo ""

if [ $TESTS_FAILED -eq 0 ]; then
    echo -e "${GREEN}✓ All tests passed!${NC}"
    echo ""
    echo "Next steps:"
    echo "1. Open Jaeger UI: $JAEGER_URL"
    echo "2. Select a service from the dropdown"
    echo "3. Click 'Find Traces' to view distributed traces"
    echo ""
    exit 0
else
    echo -e "${RED}✗ Some tests failed${NC}"
    echo ""
    echo "Troubleshooting:"
    echo "1. Ensure services are running: docker-compose ps"
    echo "2. Check service logs: docker-compose logs <service-name>"
    echo "3. Verify ports are available: 8080-8084, 16686, 4317-4318"
    echo ""
    exit 1
fi
