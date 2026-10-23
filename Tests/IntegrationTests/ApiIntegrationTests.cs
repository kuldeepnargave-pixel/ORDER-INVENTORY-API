using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Integration tests that do NOT require a database can run anywhere.
/// Authentication middleware rejects unauthenticated requests before any
/// database access, so this test runs with or without SQL Server.
/// </summary>
public class ProductsEndpointTests : IClassFixture<OrderApiFactory>
{
    private readonly OrderApiFactory _factory;
    private readonly HttpClient _client;

    public ProductsEndpointTests(OrderApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetProducts_WithoutToken_Returns401Unauthorized()
    {
        var response = await _client.GetAsync("/api/products?page=1&pageSize=5");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

/// <summary>
/// The login endpoint is rate limited to 5 requests/minute/IP.
/// Requests 1-5 produce whatever business result applies (401 for wrong
/// credentials, or 500 when no database is configured); request 6 must always
/// be rejected by the rate limiter with HTTP 429 - no database required.
/// </summary>
public class LoginRateLimitTests : IClassFixture<OrderApiFactory>
{
    private readonly HttpClient _client;

    public LoginRateLimitTests(OrderApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_SixRapidRequests_SixthIsRateLimited429()
    {
        var body = new { email = "ratelimit@test.com", password = "Whatever@123" };

        HttpStatusCode? lastStatus = null;
        for (var i = 0; i < 6; i++)
        {
            var response = await _client.PostAsJsonAsync("/api/auth/login", body);
            lastStatus = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastStatus);
    }
}

/// <summary>
/// THE concurrency test: two simultaneous orders fight over the last unit of a
/// product. Expected: exactly one 201, exactly one 409, final stock 0, and
/// exactly one order created. Requires a real SQL Server (TEST_SQL_CONNECTION_STRING)
/// because optimistic rowversion concurrency is a SQL Server feature here.
/// </summary>
public class OrderConcurrencyTests : IClassFixture<OrderApiFactory>
{
    private readonly OrderApiFactory _factory;

    public OrderConcurrencyTests(OrderApiFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task TwoConcurrentOrdersForLastUnit_ExactlyOneSucceeds_OneConflicts()
    {
        Skip.IfNot(_factory.UsesSqlServer,
            "SKIPPED: SQL Server test database not configured. Set TEST_SQL_CONNECTION_STRING (see README " +
            "'Integration test database'). This test deliberately refuses to run against InMemory/SQLite " +
            "providers because they cannot reproduce SQL Server rowversion concurrency behaviour.");

        await _factory.ResetDatabaseAsync();
        var (token, productId) = await _factory.SeedCustomerAndProductAsync(stockQuantity: 1);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        HttpRequestMessage MakeOrderRequest(string idempotencyKey)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders");
            request.Headers.Add("Idempotency-Key", idempotencyKey);
            request.Content = JsonContent.Create(new
            {
                items = new[] { new { productId, quantity = 1 } }
            });
            return request;
        }

        // Fire the two competing requests at the same time.
        var task1 = client.SendAsync(MakeOrderRequest(Guid.NewGuid().ToString("n")));
        var task2 = client.SendAsync(MakeOrderRequest(Guid.NewGuid().ToString("n")));
        await Task.WhenAll(task1, task2);

        var status1 = (int)task1.Result.StatusCode;
        var status2 = (int)task2.Result.StatusCode;

        // Exactly one 201 and exactly one 409 (which one wins is not deterministic).
        var statusCodes = new[] { status1, status2 };
        Assert.Equal(1, statusCodes.Count(s => s == 201));
        Assert.Equal(1, statusCodes.Count(s => s == 409));
    }

    [SkippableFact]
    public async Task TwoConcurrentOrdersForLastUnit_FinalStateIsConsistent()
    {
        Skip.IfNot(_factory.UsesSqlServer,
            "SKIPPED: SQL Server test database not configured (set TEST_SQL_CONNECTION_STRING, see README).");

        await _factory.ResetDatabaseAsync();
        var (token, productId) = await _factory.SeedCustomerAndProductAsync(stockQuantity: 1);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        HttpRequestMessage MakeOrderRequest(string idempotencyKey)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders");
            request.Headers.Add("Idempotency-Key", idempotencyKey);
            request.Content = JsonContent.Create(new
            {
                items = new[] { new { productId, quantity = 1 } }
            });
            return request;
        }

        var responses = await Task.WhenAll(
            client.SendAsync(MakeOrderRequest(Guid.NewGuid().ToString("n"))),
            client.SendAsync(MakeOrderRequest(Guid.NewGuid().ToString("n"))));

        var statusCodes = responses.Select(r => (int)r.StatusCode).ToList();

        // Exactly one 201 and exactly one 409.
        Assert.Equal(1, statusCodes.Count(s => s == 201));
        Assert.Equal(1, statusCodes.Count(s => s == 409));

        // Final stock must be zero - never negative.
        var productResponse = await client.GetAsync($"/api/products/{productId}");
        productResponse.EnsureSuccessStatusCode();
        var product = await productResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, product.GetProperty("stockQuantity").GetInt32());

        // Exactly one successful order exists for the competing requests.
        var myOrdersResponse = await client.GetAsync("/api/orders/my");
        myOrdersResponse.EnsureSuccessStatusCode();
        var orders = await myOrdersResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, orders.GetArrayLength());
    }
}
