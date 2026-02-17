# Smart Delivery Routing & Package Tracking System

A microservices-based delivery management system built with ASP.NET Core 8.0

## 🏗️ Architecture

### Three Microservices:

1. **AuthService** (Port 5001) - Centralized Authentication
   - User registration with BCrypt password encryption
   - JWT token generation
   - User management (Admin, Manager, Driver roles)

2. **RouteService** (Port 5002) - Route Management
   - Route CRUD operations
   - Driver assignment
   - Overlap detection
   - Search & pagination

3. **PackageService** (Port 5003) - Package Tracking
   - Package CRUD operations  
   - Status tracking (Pending → InTransit → Delivered)
   - Cross-service route validation
   - Tracking by tracking number

## 🛠 Tech Stack

- ASP.NET Core 8.0
- Entity Framework Core (In-Memory)
- JWT Bearer Authentication
- BCrypt.Net (Password Hashing)
- Serilog (Structured Logging)
- Swagger/OpenAPI
- xUnit (Testing)

## 📋 Prerequisites

- .NET 8.0 SDK
- Visual Studio 2022 / VS Code / Rider
- Postman (optional, for API testing)

## 🚀 Quick Start

### 1. Clone and Build

```bash
# Navigate to each service directory and restore packages
cd AuthService
dotnet restore
dotnet build

cd ../RouteService
dotnet restore
dotnet build

cd ../PackageService
dotnet restore
dotnet build
```

### 2. Run Services (3 separate terminals)

**Terminal 1 - AuthService:**
```bash
cd AuthService
dotnet run
# Runs on http://localhost:5001
```

**Terminal 2 - RouteService:**
```bash
cd RouteService
dotnet run
# Runs on http://localhost:5002
```

**Terminal 3 - PackageService:**
```bash
cd PackageService
dotnet run
# Runs on http://localhost:5003
```

### 3. Access Swagger UI

- AuthService: http://localhost:5001/swagger
- RouteService: http://localhost:5002/swagger
- PackageService: http://localhost:5003/swagger

## 📝 Testing Workflow

### Step 1: Register Users

**Register Admin:**
```json
POST http://localhost:5001/api/auth/register
Content-Type: application/json

{
    "username": "admin",
    "email": "admin@delivery.com",
    "password": "Admin@123",
    "role": "Admin"
}
```

**Register Driver:**
```json
POST http://localhost:5001/api/auth/register
Content-Type: application/json

{
    "username": "john_driver",
    "email": "john@delivery.com",
    "password": "Driver@123",
    "role": "Driver",
    "driverId": 101
}
```

### Step 2: Login

```json
POST http://localhost:5001/api/auth/login
Content-Type: application/json

{
    "username": "admin",
    "password": "Admin@123"
}
```

**Save the token from response!**

### Step 3: Create Route

```json
POST http://localhost:5002/api/routes
Authorization: Bearer {your-admin-token}
Content-Type: application/json

{
    "driverId": 101,
    "vehicleId": 1,
    "startLocation": "Warehouse A",
    "endLocation": "Downtown",
    "estimatedDistanceKm": 15.5,
    "scheduledDate": "2026-02-15T08:00:00Z"
}
```

### Step 4: Create Package

```json
POST http://localhost:5003/api/packages
Authorization: Bearer {your-admin-token}
Content-Type: application/json

{
    "customerId": 100,
    "routeId": 1,
    "weightKg": 3.2,
    "description": "Electronics"
}
```

### Step 5: Update Package Status (as Driver)

```json
# First login as driver
POST http://localhost:5001/api/auth/login
{
    "username": "john_driver",
    "password": "Driver@123"
}

# Update status
PATCH http://localhost:5003/api/packages/1/status
Authorization: Bearer {driver-token}
Content-Type: application/json

{
    "status": "InTransit"
}
```

## 👥 User Roles & Permissions

| Action | Admin | Manager | Driver |
|--------|-------|---------|--------|
| View all routes | ✅ | ✅ | ❌ |
| View own routes | ✅ | ✅ | ✅ |
| Create routes | ✅ | ✅ | ❌ |
| Delete routes | ✅ | ❌ | ❌ |
| Assign drivers | ✅ | ✅ | ❌ |
| View all packages | ✅ | ✅ | ❌ |
| Update package status | ✅ | ✅ | ✅ (own routes) |
| Manage users | ✅ | ❌ | ❌ |

## 🧪 Running Tests

```bash
cd Tests
dotnet test
```

## 📊 Logging

All services use Serilog with multiple sinks:

- **Console**: Real-time log output
- **File**: Daily rolling logs in `logs/` directory (30-day retention)

Log locations:
- AuthService: `logs/authservice-YYYYMMDD.log`
- RouteService: `logs/routeservice-YYYYMMDD.log`
- PackageService: `logs/packageservice-YYYYMMDD.log`

## 🔐 Security Features

- **Password Hashing**: BCrypt with 12 salt rounds
- **JWT Tokens**: 8-hour expiration
- **Account Lockout**: 5 failed attempts = 15-minute lockout
- **Role-Based Authorization**: Granular permissions per endpoint
- **Password Validation**: Min 8 chars with uppercase, lowercase, digit, special char

## ⚠️ Error Handling

Global exception middleware handles:
- 400 Bad Request (Validation errors)
- 401 Unauthorized (Invalid credentials, no token)
- 403 Forbidden (Insufficient permissions)
- 404 Not Found (Resource not found)
- 500 Internal Server Error (Unexpected errors)

## 🐳 Docker Support (Optional)

```bash
# Build and run with Docker Compose
docker-compose up -d

# View logs
docker-compose logs -f

# Stop services
docker-compose down
```

## 📁 Project Structure

```
SmartDeliverySystem/
├── AuthService/
│   ├── Controllers/
│   ├── Data/
│   ├── DTOs/
│   ├── Middleware/
│   ├── Models/
│   ├── Services/
│   └── Program.cs
├── RouteService/
│   ├── Controllers/
│   ├── Data/
│   ├── DTOs/
│   ├── Models/
│   ├── Repositories/
│   ├── Services/
│   └── Program.cs
├── PackageService/
│   ├── Controllers/
│   ├── Data/
│   ├── DTOs/
│   ├── Models/
│   ├── Repositories/
│   ├── Services/
│   └── Program.cs
└── Tests/
    ├── AuthService.Tests/
    ├── RouteService.Tests/
    └── PackageService.Tests/
```

## 🎯 Business Rules

### Routes
- EstimatedDistanceKm must be > 0
- No overlapping routes for same driver on same date
- Scheduled date cannot be in past
- Only Admin/Manager can assign drivers

### Packages
- WeightKg must be > 0
- Route must exist (validated via RouteService)
- Status transitions: Pending → InTransit → Delivered
- Cannot mark Delivered unless route date ≤ today
- Drivers can only update packages on their routes

## 🔍 Key Features Implemented

✅ Three microservices architecture  
✅ Centralized JWT authentication  
✅ BCrypt password encryption  
✅ Role-based authorization (Admin, Manager, Driver)  
✅ Serilog structured logging  
✅ Global exception handling  
✅ Cross-service communication (HTTP)  
✅ Pagination & search  
✅ Async/await throughout  
✅ In-memory EF Core databases  
✅ Swagger documentation  
✅ Unit testing  

## 📞 API Endpoints

### AuthService (Port 5001)
- POST `/api/auth/register` - Register user
- POST `/api/auth/login` - Login
- GET `/api/auth/users` - Get all users (Admin)
- GET `/api/auth/users/{id}` - Get user (Admin)
- PUT `/api/auth/users/{id}` - Update user (Admin)
- DELETE `/api/auth/users/{id}` - Deactivate user (Admin)

### RouteService (Port 5002)
- GET `/api/routes` - Get routes (paginated)
- GET `/api/routes/{id}` - Get route by ID
- POST `/api/routes` - Create route (Admin/Manager)
- PUT `/api/routes/{id}` - Update route (Admin/Manager)
- DELETE `/api/routes/{id}` - Delete route (Admin)
- POST `/api/routes/{id}/assign-driver` - Assign driver (Admin/Manager)
- GET `/api/routes/search` - Search routes

### PackageService (Port 5003)
- GET `/api/packages` - Get packages (paginated)
- GET `/api/packages/{id}` - Get package by ID
- POST `/api/packages` - Create package
- PUT `/api/packages/{id}` - Update package
- DELETE `/api/packages/{id}` - Delete package (Admin)
- PATCH `/api/packages/{id}/status` - Update status
- GET `/api/packages/tracking/{trackingNumber}` - Track package
- GET `/api/packages/search` - Search packages

## 🤝 Contributing

This is a test project demonstrating microservices architecture with .NET 8.0.

## 📄 License

MIT License
