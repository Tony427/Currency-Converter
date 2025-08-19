using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CurrencyConverter.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Authentication;
using CurrencyConverter.Domain.Interfaces;

namespace CurrencyConverter.Tests.Integration
{
    public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>
        where TProgram : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            
            builder.ConfigureServices(services =>
            {
                // Remove the app's ApplicationDbContext registration.
                var dbContextDescriptors = services.Where(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                         d.ServiceType == typeof(ApplicationDbContext)).ToList();
                
                foreach (var descriptor in dbContextDescriptors)
                {
                    services.Remove(descriptor);
                }

                // Add ApplicationDbContext using an in-memory database for testing.
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase("InMemoryDbForTesting");
                });

                // Add HttpContextAccessor for testing
                services.AddHttpContextAccessor();

                // Replace authentication with test authentication
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", options => { });

                // Replace CurrencyProvider with mock for testing
                var currencyProviderDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ICurrencyProvider));
                if (currencyProviderDescriptor != null)
                {
                    services.Remove(currencyProviderDescriptor);
                }
                services.AddScoped<ICurrencyProvider, MockCurrencyProvider>();

                // Build the service provider.
                var sp = services.BuildServiceProvider();

                // Create a scope to obtain a reference to the database
                // context (ApplicationDbContext).
                using (var scope = sp.CreateScope())
                {
                    var scopedServices = scope.ServiceProvider;
                    var db = scopedServices.GetRequiredService<ApplicationDbContext>();
                    var logger = scopedServices
                        .GetRequiredService<ILogger<CustomWebApplicationFactory<TProgram>>>();

                    // Ensure the database is created.
                    db.Database.EnsureCreated();

                    try
                    {
                        // Seed the database with test data.
                        // Utilities.InitializeDbForTests(db);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "An error occurred seeding the " +
                            "database with test messages. Error: {Message}", ex.Message);
                    }
                }
            });

            builder.ConfigureAppConfiguration((context, conf) =>
            {
                conf.AddJsonFile("appsettings.Test.json", optional: true, reloadOnChange: true);
            });
        }
    }
}
