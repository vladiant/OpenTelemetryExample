db.createUser({
  user: "appuser",
  pwd: "apppassword",
  roles: [
    { role: "readWrite", db: "dotnet_telemetry" }
  ]
});

// Switch to the application database
db = db.getSiblingDB("dotnet_telemetry");

// Create initial collections if needed
db.createCollection("weather");
