# Use the official .NET SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
WORKDIR /app

# Copy solution file and project files
COPY Currency-Converter.sln ./
COPY CurrencyConverter.API/CurrencyConverter.API.csproj ./CurrencyConverter.API/
COPY CurrencyConverter.Application/CurrencyConverter.Application.csproj ./CurrencyConverter.Application/
COPY CurrencyConverter.Domain/CurrencyConverter.Domain.csproj ./CurrencyConverter.Domain/
COPY CurrencyConverter.Infrastructure/CurrencyConverter.Infrastructure.csproj ./CurrencyConverter.Infrastructure/
COPY CurrencyConverter.Tests/CurrencyConverter.Tests.csproj ./CurrencyConverter.Tests/

# Restore dependencies
RUN dotnet restore

# Copy all source code
COPY . ./

# Build the application
RUN dotnet publish CurrencyConverter.API -c Release -o out --no-restore

# Use the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Create data directory for SQLite database
RUN mkdir -p /app/data

# Copy the published application
COPY --from=build-env /app/out .

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

# Expose port
EXPOSE 8080

# Create a non-root user for security
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8080/api/v1/health || exit 1

# Start the application
ENTRYPOINT ["dotnet", "CurrencyConverter.API.dll"]