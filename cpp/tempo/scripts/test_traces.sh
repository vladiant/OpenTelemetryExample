#!/bin/bash
# Test script to generate traces across all microservices

API_GATEWAY="http://localhost:8000"

echo "=== Testing Grafana Tempo Tracing Demo (C++) ==="
echo ""

# Health checks
echo "1. Health Checks"
echo "----------------"
echo "API Gateway:"
curl -s "$API_GATEWAY/health" | jq .
echo ""

echo "2. Get Inventory"
echo "----------------"
curl -s "$API_GATEWAY/inventory" | jq .
echo ""

echo "3. Get Single Product"
echo "---------------------"
curl -s "$API_GATEWAY/inventory/demo-product" | jq .
echo ""

echo "4. Create Order (generates distributed trace)"
echo "----------------------------------------------"
curl -s -X POST "$API_GATEWAY/orders" \
  -H "Content-Type: application/json" \
  -d '{"product_id": "demo-product", "quantity": 5}' | jq .
echo ""

echo "5. Create Another Order"
echo "-----------------------"
curl -s -X POST "$API_GATEWAY/orders" \
  -H "Content-Type: application/json" \
  -d '{"product_id": "laptop-001", "quantity": 2}' | jq .
echo ""

echo "6. Create Order with Insufficient Inventory (error trace)"
echo "----------------------------------------------------------"
curl -s -X POST "$API_GATEWAY/orders" \
  -H "Content-Type: application/json" \
  -d '{"product_id": "demo-product", "quantity": 1000}' | jq .
echo ""

echo "=== Traces Generated! ==="
echo ""
echo "View traces in Grafana:"
echo "  1. Open http://localhost:3000"
echo "  2. Go to Explore (compass icon)"
echo "  3. Select 'Tempo' datasource"
echo "  4. Use 'Search' tab to find traces"
echo "  5. Filter by service: api-gateway, order-service, inventory-service"
