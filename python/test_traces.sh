#!/bin/bash

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${BLUE}OpenTelemetry Trace Propagation Demo - Test Script${NC}\n"

# Wait for services to be ready
echo -e "${YELLOW}Waiting for services to start...${NC}"
sleep 5

echo -e "\n${GREEN}Test 1: Simple User Lookup${NC}"
echo "This demonstrates trace propagation: API Gateway -> User Service -> Database Service"
echo "Command: curl http://localhost:8000/users/123"
curl -s http://localhost:8000/users/123 | jq '.' 2>/dev/null || curl -s http://localhost:8000/users/123
echo -e "\n"

sleep 2

echo -e "${GREEN}Test 2: Create Order (Complex Flow)${NC}"
echo "This demonstrates complex trace propagation through multiple services"
echo "Command: curl -X POST 'http://localhost:8000/orders?user_id=456&product_id=1&quantity=2'"
curl -s -X POST "http://localhost:8000/orders?user_id=456&product_id=1&quantity=2" | jq '.' 2>/dev/null || curl -s -X POST "http://localhost:8000/orders?user_id=456&product_id=1&quantity=2"
echo -e "\n"

sleep 2

echo -e "${GREEN}Test 3: Another Order with Different Product${NC}"
curl -s -X POST "http://localhost:8000/orders?user_id=789&product_id=2&quantity=5" | jq '.' 2>/dev/null || curl -s -X POST "http://localhost:8000/orders?user_id=789&product_id=2&quantity=5"
echo -e "\n"

sleep 2

echo -e "${GREEN}Test 4: Get Order Details${NC}"
echo "Command: curl http://localhost:8000/orders/12345"
curl -s http://localhost:8000/orders/12345 | jq '.' 2>/dev/null || curl -s http://localhost:8000/orders/12345
echo -e "\n"

sleep 2

echo -e "${GREEN}Test 5: Direct Inventory Check${NC}"
echo "Command: curl -X POST http://localhost:8004/inventory/check"
curl -s -X POST http://localhost:8004/inventory/check \
  -H "Content-Type: application/json" \
  -d '{"product_id": 3, "quantity": 10}' | jq '.' 2>/dev/null || \
curl -s -X POST http://localhost:8004/inventory/check \
  -H "Content-Type: application/json" \
  -d '{"product_id": 3, "quantity": 10}'
echo -e "\n"

sleep 2

echo -e "${GREEN}Test 6: Multiple Users Lookup${NC}"
for i in {100..105}; do
  echo "Fetching user $i..."
  curl -s http://localhost:8000/users/$i > /dev/null
  sleep 0.5
done
echo "Done!"
echo -e "\n"

echo -e "${BLUE}All tests completed!${NC}"
echo -e "\n${YELLOW}Now open Jaeger UI to see the traces:${NC}"
echo -e "${GREEN}http://localhost:16686${NC}"
echo -e "\nSteps to view traces:"
echo "1. Select 'api-gateway' from the Service dropdown"
echo "2. Click 'Find Traces'"
echo "3. Click on any trace to see the complete distributed trace"
echo "4. Explore span details, attributes, and events"
echo -e "\n${YELLOW}Look for:${NC}"
echo "- Trace timelines showing operation durations"
echo "- Parent-child span relationships"
echo "- Service dependencies"
echo "- Custom attributes and events"
echo "- Error tracking (if any)"