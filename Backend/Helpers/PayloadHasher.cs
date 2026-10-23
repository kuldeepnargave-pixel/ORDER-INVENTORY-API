using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Backend.DTOs;

namespace Backend.Helpers;

/// <summary>
/// Produces a deterministic hash of an order-create payload so that an
/// Idempotency-Key replay can be compared against the original request.
/// Items are sorted by ProductId before hashing, so two requests containing
/// the same set of products/quantities are considered equivalent regardless
/// of the order in which the client sent them.
/// </summary>
public static class PayloadHasher
{
    public static string Hash(OrderCreateRequest request)
    {
        var normalized = new
        {
            items = (request.Items ?? new List<OrderItemRequest>())
                .OrderBy(i => i.ProductId)
                .Select(i => new { productId = i.ProductId, quantity = i.Quantity })
        };

        var json = JsonSerializer.Serialize(normalized);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}
