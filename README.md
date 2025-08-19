# Currency Converter API

A robust, scalable, and maintainable currency conversion API built with ASP.NET Core 8, providing real-time currency conversion, historical exchange rate data, and comprehensive caching mechanisms.

## Features

- **Real-time Currency Conversion**: Convert between various currencies with live exchange rates
- **Historical Exchange Rates**: Access historical currency data with pagination support
- **JWT Authentication**: Secure API access with JWT token-based authentication
- **Role-based Authorization**: User and Admin roles with appropriate permissions
- **Comprehensive Caching**: In-memory caching with configurable expiration times
- **Health Checks**: Multiple health check endpoints for monitoring
- **Rate Limiting**: Built-in rate limiting to prevent API abuse
- **Docker Support**: Full containerization with Docker and Docker Compose
- **Test Coverage**: Comprehensive unit, integration, and controller tests
- **Structured Logging**: Serilog-based logging with multiple outputs

## Technology Stack

- **.NET 9**: ASP.NET Core Web API
- **Database**: SQLite with Entity Framework Core
- **Authentication**: JWT + ASP.NET Identity
- **Caching**: In-Memory Cache (IMemoryCache)
- **Logging**: Serilog with structured logging
- **Testing**: xUnit + Moq + WebApplicationFactory
- **Containerization**: Docker + Docker Compose
- **External API**: Frankfurter API for exchange rates

## Quick Start

### Prerequisites

- .NET 9 SDK
- Docker and Docker Compose (for containerized deployment)

### Running with Docker Compose (Recommended)

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd Currency-Converter
   ```

2. **Copy environment file**
   ```bash
   cp .env.example .env
   ```
   Edit `.env` file to customize JWT secrets and other settings.

3. **Start the application**
   ```bash
   docker-compose up -d
   ```

4. **Access the API**
   - API Base URL: `http://localhost:8080`
   - Health Check: `http://localhost:8080/api/v1/health`
   - OpenAPI/Swagger (Development): `http://localhost:8080/openapi/v1.json`

5. **View logs**
   ```bash
   docker-compose logs -f api
   ```

6. **Stop the application**
   ```bash
   docker-compose down
   ```

### Running Locally (Development)

1. **Restore dependencies**
   ```bash
   dotnet restore
   ```

2. **Run database migrations**
   ```bash
   dotnet ef database update --project CurrencyConverter.Infrastructure --startup-project CurrencyConverter.API
   ```

3. **Run the application**
   ```bash
   dotnet run --project CurrencyConverter.API
   ```

4. **Run tests**
   ```bash
   dotnet test
   ```

## API Endpoints

### Authentication

| Method | Endpoint | Description | Auth Required |
|--------|----------|-------------|---------------|
| POST | `/api/v1/auth/register` | Register new user | No |
| POST | `/api/v1/auth/login` | Login user | No |

### Currency Operations

| Method | Endpoint | Description | Auth Required |
|--------|----------|-------------|---------------|
| GET | `/api/v1/rates/latest` | Get latest exchange rates | Yes (User) |
| POST | `/api/v1/rates/convert` | Convert currency amount | Yes (User) |
| GET | `/api/v1/rates/historical` | Get historical rates (paginated) | Yes (User) |

### Health & Monitoring

| Method | Endpoint | Description | Auth Required |
|--------|----------|-------------|---------------|
| GET | `/api/v1/health` | Basic health check | No |
| GET | `/api/v1/health/detailed` | Detailed health info | Yes (User) |
| GET | `/api/v1/health/system` | System information | Yes (Admin) |

## Authentication Flow

1. **Register a new user**
   ```bash
   curl -X POST http://localhost:8080/api/v1/auth/register \\
     -H "Content-Type: application/json" \\
     -d '{
       "email": "user@example.com",
       "password": "SecurePassword123",
       "firstName": "John",
       "lastName": "Doe"
     }'
   ```

2. **Login to get JWT token**
   ```bash
   curl -X POST http://localhost:8080/api/v1/auth/login \\
     -H "Content-Type: application/json" \\
     -d '{
       "email": "user@example.com",
       "password": "SecurePassword123"
     }'
   ```

3. **Use JWT token for API calls**
   ```bash
   curl -X GET http://localhost:8080/api/v1/rates/latest?base=USD \\
     -H "Authorization: Bearer <your-jwt-token>"
   ```

## Currency Operations Examples

### Get Latest Exchange Rates
```bash
curl -X GET "http://localhost:8080/api/v1/rates/latest?base=USD" \\
  -H "Authorization: Bearer <token>"
```

### Convert Currency
```bash
curl -X POST http://localhost:8080/api/v1/rates/convert \\
  -H "Authorization: Bearer <token>" \\
  -H "Content-Type: application/json" \\
  -d '{
    "amount": 100,
    "fromCurrency": "USD",
    "toCurrency": "EUR"
  }'
```

### Get Historical Rates
```bash
curl -X GET "http://localhost:8080/api/v1/rates/historical?base=USD&from=2024-01-01&to=2024-01-31&page=1&pageSize=10" \\
  -H "Authorization: Bearer <token>"
```

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Application environment | `Production` |
| `JWT_SECRET` | JWT signing secret | Required |
| `JWT_ISSUER` | JWT token issuer | `CurrencyConverterAPI` |
| `JWT_AUDIENCE` | JWT token audience | `CurrencyConverterAPI` |
| `FRANKFURTER_BASEURL` | Frankfurter API base URL | `https://api.frankfurter.app` |
| `CONNECTIONSTRING__DEFAULT` | Database connection string | Auto-configured |

### Excluded Currencies

The following currencies are excluded from all operations and will return a `400 Bad Request`:
- TRY (Turkish Lira)
- PLN (Polish Zloty)
- THB (Thai Baht)
- MXN (Mexican Peso)

### Caching Strategy

- **Latest Rates**: 15-minute expiration
- **Historical Rates**: 24-hour expiration
- **Conversion Results**: Real-time using cached rates

## Testing

### Run All Tests
```bash
dotnet test
```

### Generate Coverage Report
```bash
dotnet test --collect:"XPlat Code Coverage"
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:CoverageReport -reporttypes:Html
```

### Test Categories
- **Unit Tests**: Business logic, services, validators
- **Controller Tests**: API controllers with mocked dependencies
- **Integration Tests**: End-to-end API workflows

## Docker Configuration

### Building Docker Image
```bash
docker build -t currency-converter-api .
```

### Running with Custom Environment
```bash
docker run -p 8080:8080 \\
  -e JWT_SECRET="your-secret-key" \\
  -e ASPNETCORE_ENVIRONMENT=Production \\
  -v currency_data:/app/data \\
  currency-converter-api
```

### Volume Persistence
The SQLite database is persisted using Docker volumes:
- Volume Name: `currency_data`
- Mount Path: `/app/data`
- Database File: `/app/data/currency_converter.db`

## Development

### Project Structure
```
Currency-Converter/
├── CurrencyConverter.API/          # Web API layer
├── CurrencyConverter.Application/  # Application services
├── CurrencyConverter.Domain/       # Domain entities
├── CurrencyConverter.Infrastructure/ # Data access
├── CurrencyConverter.Tests/        # Test projects
├── docker-compose.yml             # Docker composition
├── Dockerfile                     # Container definition
└── README.md                      # This file
```

### Adding New Features

1. Follow Clean Architecture principles
2. Add appropriate unit tests
3. Update integration tests if needed
4. Update API documentation
5. Test in Docker environment

### Contributing

1. Fork the repository
2. Create a feature branch
3. Write tests for new functionality
4. Ensure all tests pass
5. Update documentation
6. Submit a pull request

## Production Deployment

### Security Considerations

1. **JWT Secret**: Use a strong, randomly generated secret
2. **HTTPS**: Enable HTTPS in production environments
3. **Rate Limiting**: Configure appropriate rate limits
4. **CORS**: Restrict CORS to specific domains
5. **Database**: Consider using a production database (PostgreSQL, SQL Server)

### Performance Tuning

1. **Caching**: Monitor cache hit rates and adjust expiration times
2. **Database**: Add indexes for frequently queried fields
3. **HTTP Client**: Configure connection pooling for external APIs
4. **Logging**: Use structured logging for better observability

### Monitoring

1. **Health Checks**: Use `/api/v1/health` for load balancer health checks
2. **Metrics**: Implement application metrics collection
3. **Logging**: Forward logs to centralized logging systems
4. **Alerting**: Set up alerts for error rates and response times

## Troubleshooting

### Common Issues

1. **Database Connection**: Ensure data directory has proper permissions
2. **JWT Errors**: Verify JWT secret and token expiration
3. **External API**: Check Frankfurter API availability
4. **Docker**: Ensure Docker daemon is running and volumes are properly mounted

### Debug Mode

Set `ASPNETCORE_ENVIRONMENT=Development` to enable:
- Detailed error messages
- Swagger/OpenAPI documentation
- Enhanced logging

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Support

For support and questions:
1. Check the health endpoints for system status
2. Review application logs for error details
3. Consult the API documentation
4. Create an issue in the repository