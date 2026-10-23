using Backend.Data;
using Backend.DTOs;
using Backend.Helpers;
using Backend.Models;
using Backend.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using Xunit;

namespace UnitTests;

/// <summary>
/// Order creation / cancellation business-rule tests.
///
/// These use the SQLite in-memory provider with the REAL AppDbContext and the
/// REAL OrderService: relational database semantics (transactions, unique
/// indexes, foreign keys, rowversion concurrency tokens) are exercised.
///
/// Note: the true "two concurrent requests fight over the last unit" scenario
/// is covered by the integration test against real SQL Server; SQLite cannot
/// reproduce SQL Server rowversion behaviour under parallel requests and is
/// deliberately not used to claim that test passes.
/// </summary>
public class OrderServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public OrderServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new TestRowVersionInterceptor())
            .Options;

        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    private AppDbContext CreateContext() => new(_options);

    private OrderService CreateService(AppDbContext db) =>
        new(db, NullLogger<OrderService>.Instance);

    public void Dispose() => _connection.Dispose();

    // ------------------------------------------------------------------
    // Seed helpers
    // ------------------------------------------------------------------

    private async Task<User> SeedUserAsync(string email = "cust@test.com", string role = UserRoles.Customer)
    {
        using var db = CreateContext();
        var user = new User { FullName = "Test Customer", Email = email, Role = role, PasswordHash = "x" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<Product> SeedProductAsync(decimal price = 100m, int stock = 10, bool isActive = true)
    {
        using var db = CreateContext();
        var product = new Product { Sku = "SKU-" + Guid.NewGuid().ToString("N")[..8], Name = "Test Product", Price = price, StockQuantity = stock, IsActive = isActive };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    private static OrderCreateRequest SingleItemRequest(int productId, int quantity) => new()
    {
        Items = new List<OrderItemRequest> { new() { ProductId = productId, Quantity = quantity } }
    };

    private static ClaimsPrincipal PrincipalFor(int userId, string role = UserRoles.Customer) =>
        new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        }, authenticationType: "TestAuth"));

    // ------------------------------------------------------------------
    // Order creation
    // ------------------------------------------------------------------

    [Fact]
    public async Task CreateOrder_ValidRequest_PersistsOrderAndDecrementsStock()
    {
        var user = await SeedUserAsync();
        var product = await SeedProductAsync(price: 100m, stock: 10);
        var service = CreateService(CreateContext());

        var result = await service.CreateOrderAsync(user.Id, SingleItemRequest(product.Id, 2), "key-success-1");

        Assert.True(result.Created);
        Assert.Equal(200m, result.Order.SubTotal);
        Assert.Equal(0m, result.Order.Discount);
        Assert.Equal(200m, result.Order.Total);
        Assert.Equal(OrderStatus.Pending, result.Order.Status);

        using var verify = CreateContext();
        Assert.Equal(8, (await verify.Products.SingleAsync(p => p.Id == product.Id)).StockQuantity);
        Assert.Equal(1, await verify.Orders.CountAsync());
        Assert.Equal(1, await verify.OrderItems.CountAsync());
    }

    [Fact]
    public async Task CreateOrder_FifthOrderByCustomer_AppliesTenPercentDiscount()
    {
        var user = await SeedUserAsync();
        var product = await SeedProductAsync(price: 100m, stock: 50);
        var service = CreateService(CreateContext());

        for (var i = 1; i <= 4; i++)
        {
            await service.CreateOrderAsync(user.Id, SingleItemRequest(product.Id, 1), $"key-seq-{i}");
        }

        var fifth = await service.CreateOrderAsync(user.Id, SingleItemRequest(product.Id, 1), "key-seq-5");

        // Subtotal 100 -> no 5% rule; sequence 5 -> additional 10% = 10.
        Assert.Equal(100m, fifth.Order.SubTotal);
        Assert.Equal(10m, fifth.Order.Discount);
        Assert.Equal(90m, fifth.Order.Total);
    }

    [Fact]
    public async Task CreateOrder_SubtotalAboveThreshold_AppliesFivePercentDiscount()
    {
        var user = await SeedUserAsync();
        var product = await SeedProductAsync(price: 2_500m, stock: 50);
        var service = CreateService(CreateContext());

        var result = await service.CreateOrderAsync(user.Id, SingleItemRequest(product.Id, 5), "key-threshold");

        // Subtotal 12,500 -> 5% = 625. First order -> no fifth-order bonus.
        Assert.Equal(12_500m, result.Order.SubTotal);
        Assert.Equal(625m, result.Order.Discount);
        Assert.Equal(11_875m, result.Order.Total);
    }

    [Fact]
    public async Task CreateOrder_InsufficientStock_ThrowsAndPersistsNothing()
    {
        var user = await SeedUserAsync();
        var product = await SeedProductAsync(price: 100m, stock: 2);
        var service = CreateService(CreateContext());

        var ex = await Assert.ThrowsAsync<OrderValidationException>(
            () => service.CreateOrderAsync(user.Id, SingleItemRequest(product.Id, 5), "key-stock"));

        Assert.Contains("Insufficient stock", ex.Message);

        using var verify = CreateContext();
        Assert.Equal(2, (await verify.Products.SingleAsync(p => p.Id == product.Id)).StockQuantity);
        Assert.Equal(0, await verify.Orders.CountAsync());
    }

    [Fact]
    public async Task CreateOrder_InactiveProduct_Throws()
    {
        var user = await SeedUserAsync();
        var product = await SeedProductAsync(stock: 10, isActive: false);
        var service = CreateService(CreateContext());

        var ex = await Assert.ThrowsAsync<OrderValidationException>(
            () => service.CreateOrderAsync(user.Id, SingleItemRequest(product.Id, 1), "key-inactive"));

        Assert.Contains("inactive", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateOrder_InvalidQuantity_Throws(int quantity)
    {
        var user = await SeedUserAsync();
        var product = await SeedProductAsync(stock: 10);
        var service = CreateService(CreateContext());

        await Assert.ThrowsAsync<OrderValidationException>(
            () => service.CreateOrderAsync(user.Id, SingleItemRequest(product.Id, quantity), $"key-qty-{quantity}"));
    }

    [Fact]
    public async Task CreateOrder_DuplicateProductReferences_Throws()
    {
        var user = await SeedUserAsync();
        var product = await SeedProductAsync(stock: 10);
        var service = CreateService(CreateContext());

        var request = new OrderCreateRequest
        {
            Items = new List<OrderItemRequest>
            {
                new() { ProductId = product.Id, Quantity = 1 },
                new() { ProductId = product.Id, Quantity = 2 }
            }
        };

        await Assert.ThrowsAsync<OrderValidationException>(
            () => service.CreateOrderAsync(user.Id, request, "key-dup"));
    }

    [Fact]
    public async Task CreateOrder_EmptyItems_Throws()
    {
        var user = await SeedUserAsync();
        var service = CreateService(CreateContext());

        await Assert.ThrowsAsync<OrderValidationException>(
            () => service.CreateOrderAsync(user.Id, new OrderCreateRequest { Items = new List<OrderItemRequest>() }, "key-empty"));
    }

    // ------------------------------------------------------------------
    // Transaction rollback: the database itself rejects the save mid-way
    // (via a SQLite trigger), proving the explicit transaction + implicit
    // SaveChanges transaction roll back the stock reservation together.
    // ------------------------------------------------------------------

    [Fact]
    public async Task CreateOrder_DatabaseFailureMidSave_RollsBackStockAndOrder()
    {
        var user = await SeedUserAsync();
        var productA = await SeedProductAsync(price: 100m, stock: 1_000);
        var productB = await SeedProductAsync(price: 200m, stock: 1_000);

        using (var db = CreateContext())
        {
            // Force the database to reject the OrderItems insert.
            db.Database.ExecuteSqlRaw(
                @"CREATE TRIGGER trg_force_orderitem_failure BEFORE INSERT ON OrderItems
                  WHEN NEW.Quantity = 777
                  BEGIN SELECT RAISE(ABORT, 'Forced failure for rollback test'); END;");
        }

        var service = CreateService(CreateContext());
        var request = new OrderCreateRequest
        {
            Items = new List<OrderItemRequest>
            {
                new() { ProductId = productA.Id, Quantity = 777 }, // passes validation (stock 1000), triggers DB failure at save time
                new() { ProductId = productB.Id, Quantity = 1 }
            }
        };

        await Assert.ThrowsAsync<DbUpdateException>(
            () => service.CreateOrderAsync(user.Id, request, "key-rollback"));

        using var verify = CreateContext();
        Assert.Equal(1_000, (await verify.Products.SingleAsync(p => p.Id == productA.Id)).StockQuantity);
        Assert.Equal(1_000, (await verify.Products.SingleAsync(p => p.Id == productB.Id)).StockQuantity);
        Assert.Equal(0, await verify.Orders.CountAsync());
        Assert.Equal(0, await verify.OrderItems.CountAsync());
    }

    // ------------------------------------------------------------------
    // Idempotency
    // ------------------------------------------------------------------

    [Fact]
    public async Task CreateOrder_SameKeySamePayload_ReturnsExistingOrderWithoutDoubleReservation()
    {
        var user = await SeedUserAsync();
        var product = await SeedProductAsync(price: 100m, stock: 10);
        var service = CreateService(CreateContext());
        var request = SingleItemRequest(product.Id, 2);

        var first = await service.CreateOrderAsync(user.Id, request, "key-idem-1");
        var replay = await service.CreateOrderAsync(user.Id, request, "key-idem-1");

        Assert.True(first.Created);
        Assert.False(replay.Created);
        Assert.Equal(first.Order.Id, replay.Order.Id);

        using var verify = CreateContext();
        Assert.Equal(8, (await verify.Products.SingleAsync(p => p.Id == product.Id)).StockQuantity); // reserved once
        Assert.Equal(1, await verify.Orders.CountAsync());
    }

    [Fact]
    public async Task CreateOrder_SameKeyDifferentPayload_ThrowsConflict()
    {
        var user = await SeedUserAsync();
        var product = await SeedProductAsync(price: 100m, stock: 10);
        var service = CreateService(CreateContext());

        await service.CreateOrderAsync(user.Id, SingleItemRequest(product.Id, 2), "key-mismatch");

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => service.CreateOrderAsync(user.Id, SingleItemRequest(product.Id, 3), "key-mismatch"));

        Assert.Contains("different payload", ex.Message);

        using var verify = CreateContext();
        Assert.Equal(1, await verify.Orders.CountAsync());
    }

    [Fact]
    public async Task CreateOrder_FailedValidation_DoesNotConsumeTheIdempotencyKey()
    {
        var user = await SeedUserAsync();
        var product = await SeedProductAsync(price: 100m, stock: 2);
        var service = CreateService(CreateContext());

        // First attempt fails validation (insufficient stock)...
        await Assert.ThrowsAsync<OrderValidationException>(
            () => service.CreateOrderAsync(user.Id, SingleItemRequest(product.Id, 5), "key-retry"));

        // ...so the same key can be reused successfully with a valid quantity.
        var retry = await service.CreateOrderAsync(user.Id, SingleItemRequest(product.Id, 2), "key-retry");

        Assert.True(retry.Created);
        Assert.Equal(200m, retry.Order.Total);
    }

    // ------------------------------------------------------------------
    // Status transitions / cancellation / stock restoration
    // ------------------------------------------------------------------

    [Fact]
    public async Task UpdateStatus_CancelPendingOrder_RestoresStock()
    {
        var user = await SeedUserAsync();
        var product = await SeedProductAsync(price: 100m, stock: 10);
        var service = CreateService(CreateContext());

        var order = (await service.CreateOrderAsync(user.Id, SingleItemRequest(product.Id, 2), "key-cancel-1")).Order;

        var cancelled = await service.UpdateStatusAsync(order.Id, PrincipalFor(user.Id), OrderStatus.Cancelled);

        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        using var verify = CreateContext();
        Assert.Equal(10, (await verify.Products.SingleAsync(p => p.Id == product.Id)).StockQuantity);
    }

    [Fact]
    public async Task UpdateStatus_CancelTwice_SecondAttemptRejected_StockRestoredExactlyOnce()
    {
        var user = await SeedUserAsync();
        var product = await SeedProductAsync(price: 100m, stock: 10);
        var service = CreateService(CreateContext());

        var order = (await service.CreateOrderAsync(user.Id, SingleItemRequest(product.Id, 2), "key-cancel-2")).Order;
        await service.UpdateStatusAsync(order.Id, PrincipalFor(user.Id), OrderStatus.Cancelled);

        // Second cancellation: Cancelled is terminal -> 400, stock untouched again.
        var ex = await Assert.ThrowsAsync<OrderValidationException>(
            () => service.UpdateStatusAsync(order.Id, PrincipalFor(user.Id), OrderStatus.Cancelled));

        Assert.Contains("terminal", ex.Message, StringComparison.OrdinalIgnoreCase);
        using var verify = CreateContext();
        Assert.Equal(10, (await verify.Products.SingleAsync(p => p.Id == product.Id)).StockQuantity);
    }

    [Fact]
    public async Task UpdateStatus_InvalidTransition_Throws()
    {
        var user = await SeedUserAsync();
        var product = await SeedProductAsync(price: 100m, stock: 10);
        var service = CreateService(CreateContext());

        var order = (await service.CreateOrderAsync(user.Id, SingleItemRequest(product.Id, 1), "key-invalid")).Order;

        // Pending -> Shipped is not allowed (admin performs the change so that
        // the transition rule itself is exercised; customers attempting a
        // non-cancel change are rejected earlier with 403 - see the next test).
        var ex = await Assert.ThrowsAsync<OrderValidationException>(
            () => service.UpdateStatusAsync(order.Id, PrincipalFor(999, UserRoles.Admin), OrderStatus.Shipped));
        Assert.Contains("cannot change", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateStatus_CustomerAttemptingNonCancelChange_IsRejectedBeforeValidation()
    {
        var user = await SeedUserAsync();
        var product = await SeedProductAsync(price: 100m, stock: 10);
        var service = CreateService(CreateContext());

        var order = (await service.CreateOrderAsync(user.Id, SingleItemRequest(product.Id, 1), "key-cust-rule")).Order;

        // Customers may only cancel; this authorization check happens even if
        // the requested transition would also be invalid.
        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.UpdateStatusAsync(order.Id, PrincipalFor(user.Id), OrderStatus.Shipped));
    }

    [Fact]
    public async Task UpdateStatus_CustomerCannotConfirmOrder_Forbidden()
    {
        var user = await SeedUserAsync();
        var product = await SeedProductAsync(price: 100m, stock: 10);
        var service = CreateService(CreateContext());

        var order = (await service.CreateOrderAsync(user.Id, SingleItemRequest(product.Id, 1), "key-forbid")).Order;

        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.UpdateStatusAsync(order.Id, PrincipalFor(user.Id), OrderStatus.Confirmed));
    }

    [Fact]
    public async Task UpdateStatus_AdminCanConfirmOrder()
    {
        var user = await SeedUserAsync();
        var product = await SeedProductAsync(price: 100m, stock: 10);
        var service = CreateService(CreateContext());

        var order = (await service.CreateOrderAsync(user.Id, SingleItemRequest(product.Id, 1), "key-admin")).Order;

        var confirmed = await service.UpdateStatusAsync(order.Id, PrincipalFor(999, UserRoles.Admin), OrderStatus.Confirmed);
        Assert.Equal(OrderStatus.Confirmed, confirmed.Status);
    }

    [Fact]
    public async Task UpdateStatus_CustomerCannotAccessAnotherCustomersOrder()
    {
        var owner = await SeedUserAsync("owner@test.com");
        var other = await SeedUserAsync("other@test.com");
        var product = await SeedProductAsync(price: 100m, stock: 10);
        var service = CreateService(CreateContext());

        var order = (await service.CreateOrderAsync(owner.Id, SingleItemRequest(product.Id, 1), "key-owner")).Order;

        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.GetOrderAsync(order.Id, PrincipalFor(other.Id)));
    }

    // ------------------------------------------------------------------
    // Optimistic concurrency (rowversion) with the relational provider
    // ------------------------------------------------------------------

    [Fact]
    public async Task ProductRowVersion_ConcurrentStaleUpdate_ThrowsConcurrencyException()
    {
        await SeedUserAsync();
        var product = await SeedProductAsync(price: 100m, stock: 10);

        // Context A loads the product...
        using var ctxA = CreateContext();
        var pA = await ctxA.Products.SingleAsync(p => p.Id == product.Id);

        // ...context B updates and saves it first...
        using (var ctxB = CreateContext())
        {
            var pB = await ctxB.Products.SingleAsync(p => p.Id == product.Id);
            pB.StockQuantity += 5;
            await ctxB.SaveChangesAsync();
        }

        // ...then A's stale update must be rejected by the rowversion token.
        pA.Name = "Stale update attempt";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ctxA.SaveChangesAsync());
    }
}
