using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs;

public class OrderItemRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "productId must be a positive number.")]
    public int ProductId { get; set; }

    [Range(1, 10_000, ErrorMessage = "quantity must be at least 1.")]
    public int Quantity { get; set; }
}

public class OrderCreateRequest
{
    [Required, MinLength(1, ErrorMessage = "The order must contain at least one item.")]
    public List<OrderItemRequest> Items { get; set; } = new();
}

public class OrderStatusUpdateRequest
{
    [Required]
    public string Status { get; set; } = string.Empty;
}

public class OrderItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class OrderDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
}

/// <summary>
/// Created = true  -> a new order was created (controller returns HTTP 201).
/// Created = false -> idempotent replay of a previous successful request;
///                    the existing order is returned (controller returns HTTP 200).
/// </summary>
public record OrderCreationResult(OrderDto Order, bool Created);
