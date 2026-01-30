#!/bin/bash

################################################################################
#                                                                              #
#         CURL TESTING COMMANDS - .NET Telemetry Playground                  #
#         Docker Compose Services Testing Guide                              #
#                                                                              #
################################################################################

echo "╔══════════════════════════════════════════════════════════════════════╗"
echo "║             CURL TESTING COMMANDS - Quick Reference                 ║"
echo "║            Services: Frontend (5000), Backend (5001)                 ║"
echo "║          Infrastructure: MongoDB (27017), Pulsar (6650/8080)         ║"
echo "╚══════════════════════════════════════════════════════════════════════╝"
echo ""

# ============================================================================
#  1. BASIC CONNECTIVITY TESTS
# ============================================================================
test_basic_connectivity() {
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "1. BASIC CONNECTIVITY TESTS"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    
    echo ""
    echo "Test Frontend API (Port 5000):"
    curl -I http://localhost:5000/
    
    echo ""
    echo "Test Backend API (Port 5001):"
    curl -I http://localhost:5001/
    
    echo ""
    echo "Test with verbose output:"
    curl -v http://localhost:5000/ 2>&1 | head -20
}

# ============================================================================
#  2. PULSAR ADMIN API TESTS
# ============================================================================
test_pulsar_admin() {
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "2. PULSAR ADMIN API TESTS"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    
    echo ""
    echo "List available brokers:"
    curl -s http://localhost:8080/admin/v2/brokers
    
    echo ""
    echo ""
    echo "List namespaces:"
    curl -s http://localhost:8080/admin/v2/namespaces/public
    
    echo ""
    echo ""
    echo "Get broker info:"
    curl -s http://localhost:8080/admin/v2/brokers/localhost:8080
}

# ============================================================================
#  3. API ENDPOINT TESTS
# ============================================================================
test_api_endpoints() {
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "3. API ENDPOINT TESTS"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    
    echo ""
    echo "Get all weather records (Frontend):"
    curl -X GET http://localhost:5000/api/weather \
      -H "Content-Type: application/json" 2>/dev/null | head -50
    
    echo ""
    echo ""
    echo "Get all weather records (Backend):"
    curl -X GET http://localhost:5001/api/weather \
      -H "Content-Type: application/json" 2>/dev/null | head -50
}

# ============================================================================
#  4. CREATE NEW WEATHER RECORD
# ============================================================================
test_create_weather() {
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "4. CREATE NEW WEATHER RECORD"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    
    echo ""
    echo "Create weather record (Frontend):"
    curl -X POST http://localhost:5000/api/weather \
      -H "Content-Type: application/json" \
      -d '{
        "date": "2025-12-20",
        "temperatureC": 15,
        "summary": "Sunny"
      }' 2>/dev/null
    
    echo ""
    echo ""
    echo "Create weather record (Backend):"
    curl -X POST http://localhost:5001/api/weather \
      -H "Content-Type: application/json" \
      -d '{
        "date": "2025-12-21",
        "temperatureC": 18,
        "summary": "Cloudy"
      }' 2>/dev/null
}

# ============================================================================
#  5. HEALTH CHECKS
# ============================================================================
test_health_checks() {
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "5. SERVICE HEALTH CHECKS"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    
    echo ""
    for service in "Frontend:localhost:5000" "Backend:localhost:5001" "Pulsar-Admin:localhost:8080"; do
        IFS=':' read -r name url <<< "$service"
        echo "Testing $name ($url):"
        curl -s -o /dev/null -w "  Status: %{http_code} | Response Time: %{time_total}s\n" http://$url/ 2>/dev/null
    done
}

# ============================================================================
#  6. MONGODB CONNECTION TEST
# ============================================================================
test_mongodb() {
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "6. MONGODB CONNECTION TEST"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    
    echo ""
    echo "MongoDB ping:"
    docker exec dotnet-mongo mongosh --authenticationDatabase admin -u admin -p password \
      --eval "db.adminCommand('ping')" 2>&1 | grep -E "(ok|error)"
}

# ============================================================================
#  7. SAVE RESPONSE TO FILE
# ============================================================================
test_save_response() {
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "7. SAVE RESPONSE TO FILE"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    
    echo ""
    echo "Saving API response to weather_response.json..."
    curl -s http://localhost:5000/api/weather -o weather_response.json 2>/dev/null
    echo "Saved to weather_response.json"
    echo ""
    echo "Content preview:"
    head -20 weather_response.json 2>/dev/null || echo "No response captured"
}

# ============================================================================
#  8. ERROR TESTING
# ============================================================================
test_error_handling() {
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "8. ERROR HANDLING TESTS"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    
    echo ""
    echo "Test invalid endpoint (should return 404):"
    curl -I http://localhost:5000/api/nonexistent 2>/dev/null
    
    echo ""
    echo "Test invalid JSON (should return error):"
    curl -X POST http://localhost:5000/api/weather \
      -H "Content-Type: application/json" \
      -d 'invalid json' 2>/dev/null | head -5
}

# ============================================================================
#  MAIN MENU
# ============================================================================
show_menu() {
    echo ""
    echo "════════════════════════════════════════════════════════════════════════"
    echo "SELECT TEST CATEGORY:"
    echo "════════════════════════════════════════════════════════════════════════"
    echo ""
    echo "  1) Basic Connectivity Tests"
    echo "  2) Pulsar Admin API Tests"
    echo "  3) API Endpoint Tests"
    echo "  4) Create Weather Record"
    echo "  5) Health Checks"
    echo "  6) MongoDB Connection"
    echo "  7) Save Response to File"
    echo "  8) Error Handling Tests"
    echo "  9) Run All Tests"
    echo "  0) Exit"
    echo ""
    read -p "Enter your choice [0-9]: " choice
    
    case $choice in
        1) test_basic_connectivity ;;
        2) test_pulsar_admin ;;
        3) test_api_endpoints ;;
        4) test_create_weather ;;
        5) test_health_checks ;;
        6) test_mongodb ;;
        7) test_save_response ;;
        8) test_error_handling ;;
        9) 
            test_basic_connectivity
            test_pulsar_admin
            test_api_endpoints
            test_health_checks
            test_mongodb
            ;;
        0) 
            echo "Exiting..."
            exit 0
            ;;
        *) 
            echo "Invalid choice. Please try again."
            show_menu
            ;;
    esac
}

# ============================================================================
#  RUN TESTS
# ============================================================================

# If script is run with argument, run specific test
if [ $# -gt 0 ]; then
    case $1 in
        connectivity) test_basic_connectivity ;;
        pulsar) test_pulsar_admin ;;
        api) test_api_endpoints ;;
        weather) test_create_weather ;;
        health) test_health_checks ;;
        mongodb) test_mongodb ;;
        save) test_save_response ;;
        errors) test_error_handling ;;
        all) 
            test_basic_connectivity
            test_pulsar_admin
            test_api_endpoints
            test_health_checks
            test_mongodb
            ;;
        *)
            echo "Usage: $0 {connectivity|pulsar|api|weather|health|mongodb|save|errors|all}"
            exit 1
            ;;
    esac
else
    # Show interactive menu
    show_menu
fi

echo ""
echo "════════════════════════════════════════════════════════════════════════"
echo "Test completed!"
echo "════════════════════════════════════════════════════════════════════════"
