#!/bin/bash

# This script is intended to run tests for the project.

# Navigate to the backend/api-gateway directory and run tests
echo "Running tests for API Gateway..."
cd apps/backend/api-gateway
npm test

# Navigate to the backend/customer-api directory and run tests
echo "Running tests for Customer API..."
cd ../customer-api
npm test

# Navigate to the backend/admin-api directory and run tests
echo "Running tests for Admin API..."
cd ../admin-api
npm test

echo "All tests completed."