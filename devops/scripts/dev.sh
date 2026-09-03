#!/bin/bash

# This script sets up the development environment for the project.

# Start the backend services
echo "Starting backend services..."
cd apps/backend/api-gateway && npm run dev &
cd ../customer-api && npm run dev &
cd ../admin-api && npm run dev &

# Start the frontend services (if applicable)
# Uncomment the following lines if frontend services are to be started
# echo "Starting frontend services..."
# cd ../../frontend/customer-web && npm run dev &
# cd ../admin-web && npm run dev &
# cd ../../mobile/customer-mobile && npm run dev &

# Wait for all background processes to finish
wait

echo "Development environment is up and running."