using Backend.DTOs;
using Backend.Helpers;
using Xunit;

namespace UnitTests;

public class OrderStatusTransitionTests
{
    [Theory]
    [InlineData("Pending", "Confirmed")]
    [InlineData("Pending", "Cancelled")]
    [InlineData("Confirmed", "Shipped")]
    [InlineData("Confirmed", "Cancelled")]
    public void CanTransition_ValidTransitions_ReturnsTrue(string from, string to)
    {
        Assert.True(OrderStatusTransitions.CanTransition(from, to));
    }

    [Theory]
    [InlineData("Pending", "Shipped")]      // must be confirmed first
    [InlineData("Confirmed", "Pending")]    // no going back
    [InlineData("Shipped", "Pending")]
    [InlineData("Shipped", "Confirmed")]
    [InlineData("Shipped", "Cancelled")]    // shipped orders cannot be cancelled
    [InlineData("Cancelled", "Pending")]
    [InlineData("Cancelled", "Confirmed")]
    [InlineData("Cancelled", "Shipped")]
    [InlineData("Pending", "Pending")]      // self-transition not allowed
    [InlineData("Unknown", "Confirmed")]    // unknown status
    public void CanTransition_InvalidTransitions_ReturnsFalse(string from, string to)
    {
        Assert.False(OrderStatusTransitions.CanTransition(from, to));
    }

    [Fact]
    public void AllowedTransitionsDescription_TerminalStatus_SaysNoFurtherTransitions()
    {
        var description = OrderStatusTransitions.AllowedTransitionsDescription("Cancelled");
        Assert.Contains("terminal", description, StringComparison.OrdinalIgnoreCase);
    }
}

public class PayloadHasherTests
{
    private static OrderCreateRequest Request(int productId, int quantity) => new()
    {
        Items = new List<OrderItemRequest> { new() { ProductId = productId, Quantity = quantity } }
    };

    [Fact]
    public void Hash_SamePayload_ProducesSameHash()
    {
        var a = PayloadHasher.Hash(Request(1, 2));
        var b = PayloadHasher.Hash(Request(1, 2));
        Assert.Equal(a, b);
    }

    [Fact]
    public void Hash_DifferentQuantity_ProducesDifferentHash()
    {
        Assert.NotEqual(PayloadHasher.Hash(Request(1, 2)), PayloadHasher.Hash(Request(1, 3)));
    }

    [Fact]
    public void Hash_DifferentProduct_ProducesDifferentHash()
    {
        Assert.NotEqual(PayloadHasher.Hash(Request(1, 2)), PayloadHasher.Hash(Request(2, 2)));
    }

    [Fact]
    public void Hash_SameItemsDifferentOrder_ProducesSameHash()
    {
        var first = new OrderCreateRequest
        {
            Items = new List<OrderItemRequest>
            {
                new() { ProductId = 1, Quantity = 2 },
                new() { ProductId = 2, Quantity = 1 }
            }
        };
        var second = new OrderCreateRequest
        {
            Items = new List<OrderItemRequest>
            {
                new() { ProductId = 2, Quantity = 1 },
                new() { ProductId = 1, Quantity = 2 }
            }
        };

        Assert.Equal(PayloadHasher.Hash(first), PayloadHasher.Hash(second));
    }
}
