#!/bin/bash
# MongoDB replica set initialization script

# Wait for MongoDB to be ready
sleep 5

# Initialize replica set
mongosh --authenticationDatabase admin -u admin -p password --host mongo << EOF
rs.initiate({
  _id: "rs0",
  members: [
    {
      _id: 0,
      host: "mongo:27017"
    }
  ]
});

// Create database and collections
use dotnet_telemetry;

// Wait for replica set to be ready
sleep(2000);

EOF

echo "MongoDB replica set initialized"
