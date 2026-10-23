using System.Net.Http.Json;
using Backend.Data;
using Backend.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntegrationTests;

/// <summary>
/// Test server factory. Behaviour:
///  - Runs in the "IntegrationTest" environment (no auto-migration at startup).
///  - If TEST_SQL_CONNECTION_STRING is set (see README), the API is wired to a
///    real SQL Server test database, which enables the concurrency test.
///  - If it is NOT set, endpoints that touch the database will fail with 500s;
///    only the DB-free integration tests are guaranteed to pass in that mode.
/// The concurrency test intentionally does NOT fall back to InMemory/SQLite:
/// those providers cannot reproduce SQL Server rowversion semantics.
/// </summary>
public class OrderApiFactory : WebApplicationFactory<Program>
{
    public string? SqlConnectionString { get; } = Environment.GetEnvironmentVariable("TEST_SQL_CONNECTION_STRING");

    public bool UsesSqlServer => !string.IsNullOrWhiteSpace(SqlConnectionString);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTest");

        if (UsesSqlServer)
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", SqlConnectionString);
        }
    }

    /// <summary>Apply migrations and wipe test tables (call with a dedicated test database).</summary>
    public async Task ResetDatabaseAsync()
    {
        if (!UsesSqlServer)
        {
            throw new InvalidOperationException(
                "TEST_SQL_CONNECTION_STRING is not set; there is no database to reset.");
        }

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        await db.Database.ExecuteSqlRawAsync("DELETE FROM OrderItems;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Orders;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM RefreshTokens;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Products;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Users;");
    }

    /// <summary>Seed a customer (password Customer1@123) and a product with the given stock.</summary>
    public async Task<(string accessToken, int productId)> SeedCustomerAndProductAsync(int stockQuantity)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        var customer = new User { FullName = "Race Customer", Email = "race@test.com", Role = UserRoles.Customer };
        customer.PasswordHash = hasher.HashPassword(customer, "Customer1@123");
        db.Users.Add(customer);

        var product = new Product
        {
            Sku = "SKU-RACE-" + Guid.NewGuid().ToString("N")[..6],
            Name = "Last Unit Item",
            Description = "Product used by the concurrency integration test.",
            Price = 100m,
            StockQuantity = stockQuantity,
            IsActive = true
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var client = CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new { email = customer.Email, password = "Customer1@123" });
        loginResponse.EnsureSuccessStatusCode();

        var payload = await loginResponse.Content.ReadFromJsonAsync<LoginPayload>();
        return (payload!.AccessToken, product.Id);
    }

    private sealed record LoginPayload(string AccessToken, string RefreshToken);
}
