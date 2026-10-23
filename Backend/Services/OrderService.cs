using Backend.Data;
using Backend.DTOs;
using Backend.Helpers;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Backend.Services;

public interface IOrderService
{
    Task<OrderCreationResult> CreateOrderAsync(int customerId, OrderCreateRequest request, string idempotencyKey);
    Task<List<OrderDto>> GetMyOrdersAsync(int customerId);
    Task<OrderDto> GetOrderAsync(int orderId, ClaimsPrincipal principal);
    Task<PagedResult<OrderDto>> GetAllOrdersAsync(int page, int pageSize);
    Task<OrderDto> UpdateStatusAsync(int orderId, ClaimsPrincipal principal, string newStatus);
}

public class OrderService : IOrderService
{
    private readonly AppDbContext _db;
    private readonly ILogger<OrderService> _logger;

    public OrderService(AppDbContext db, ILogger<OrderService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // Order creation: atomic stock reservation + optimistic concurrency +
    // idempotency, all inside a single database transaction.
    // -------------------------------------------------------------------------
    public async Task<OrderCreationResult> CreateOrderAsync(int customerId, OrderCreateRequest request, string idempotencyKey)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();

        // ---- Idempotency replay check --------------------------------------
        var payloadHash = PayloadHasher.Hash(request);
        var existing = await _db.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.CustomerId == customerId && o.IdempotencyKey == idempotencyKey);

        if (existing is not null)
        {
            if (!string.Equals(existing.IdempotencyPayloadHash, payloadHash, StringComparison.Ordinal))
            {
                throw new ConflictException(
                    "This Idempotency-Key was already used with a different payload. Use a new key for a new order.");
            }

            _logger.LogInformation("Idempotent replay for key {Key}: returning existing order {OrderId}.",
                idempotencyKey, existing.Id);
            return new OrderCreationResult(MapToDto(existing), Created: false);
        }

        // ---- Request + customer validation ----------------------------------
        if (request.Items is null || request.Items.Count == 0)
        {
            throw new OrderValidationException("The order must contain at least one item.");
        }

        if (request.Items.GroupBy(i => i.ProductId).Any(g => g.Count() > 1))
        {
            throw new OrderValidationException("The order contains duplicate product references. Merge quantities per product.");
        }

        if (!await _db.Users.AnyAsync(u => u.Id == customerId))
        {
            throw new UnauthorizedException("The authenticated customer no longer exists.");
        }

        // ---- Load products and validate availability -------------------------
        var productIds = request.Items.Select(i => i.ProductId).ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
            {
                throw new OrderValidationException($"Quantity for product {item.ProductId} must be greater than zero.");
            }
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                throw new OrderValidationException($"Product {item.ProductId} does not exist.");
            }
            if (!product.IsActive)
            {
                throw new OrderValidationException($"Product '{product.Name}' is inactive and cannot be ordered.");
            }
            if (product.StockQuantity < item.Quantity)
            {
                throw new OrderValidationException(
                    $"Insufficient stock for '{product.Name}'. Requested {item.Quantity}, available {product.StockQuantity}.");
            }
        }

        // ---- Build order, reserve stock, calculate totals server-side --------
        var priorOrderCount = await _db.Orders.CountAsync(o => o.CustomerId == customerId);
        var orderSequence = priorOrderCount + 1; // includes the order being created

        var order = new Order
        {
            CustomerId = customerId,
            Status = OrderStatus.Pending,
            IdempotencyKey = idempotencyKey,
            IdempotencyPayloadHash = payloadHash,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        decimal subtotal = 0m;
        foreach (var item in request.Items)
        {
            var product = products[item.ProductId];

            // Optimistic concurrency: the UPDATE generated by EF Core carries a
            // WHERE [RowVersion] = @original clause. If a competing request
            // changed this row, SaveChanges throws DbUpdateConcurrencyException
            // and the whole transaction is rolled back (HTTP 409).
            product.StockQuantity -= item.Quantity;

            var lineTotal = Money.Round(product.Price * item.Quantity);
            subtotal += lineTotal;

            var orderItem = new OrderItem
            {
                ProductId = product.Id,
                Quantity = item.Quantity,
                UnitPrice = product.Price,
                LineTotal = lineTotal
            };
            orderItem.Product = product;
            order.Items.Add(orderItem);
        }

        subtotal = Money.Round(subtotal);
        var discount = DiscountCalculator.CalculateDiscount(subtotal, orderSequence);

        order.SubTotal = subtotal;
        order.Discount = discount;
        order.Total = Money.Round(subtotal - discount);

        _db.Orders.Add(order);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request changed a product row (e.g. bought the last unit).
            throw new ConflictException(
                "Product stock was changed by another concurrent order. No order was created; please retry.");
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IdempotencyKey") == true)
        {
            // Two concurrent requests used the same Idempotency-Key: the unique
            // index UX_Orders_Customer_IdempotencyKey rejected the second one.
            var winner = await _db.Orders.AsNoTracking()
                .Include(o => o.Items).ThenInclude(i => i.Product)
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(o => o.CustomerId == customerId && o.IdempotencyKey == idempotencyKey);

            if (winner is not null && string.Equals(winner.IdempotencyPayloadHash, payloadHash, StringComparison.Ordinal))
            {
                await transaction.CommitAsync();
                return new OrderCreationResult(MapToDto(winner), Created: false);
            }

            throw new ConflictException(
                "This Idempotency-Key was already used with a different payload. Use a new key for a new order.");
        }

        await transaction.CommitAsync();
        _logger.LogInformation(
            "Order {OrderId} created for customer {CustomerId}: subtotal {SubTotal}, discount {Discount}, total {Total}.",
            order.Id, customerId, order.SubTotal, order.Discount, order.Total);

        return new OrderCreationResult(MapToDto(order), Created: true);
    }

    public async Task<List<OrderDto>> GetMyOrdersAsync(int customerId)
    {
        var orders = await _db.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.Customer)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return orders.Select(MapToDto).ToList();
    }

    public async Task<OrderDto> GetOrderAsync(int orderId, ClaimsPrincipal principal)
    {
        var order = await _db.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order is null)
        {
            throw new NotFoundException($"Order {orderId} was not found.");
        }

        EnsureCanAccess(order, principal);
        return MapToDto(order);
    }

    public async Task<PagedResult<OrderDto>> GetAllOrdersAsync(int page, int pageSize)
    {
        var query = _db.Orders.AsNoTracking();

        var totalCount = await query.CountAsync();
        var orders = await query
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.Customer)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<OrderDto>
        {
            Items = orders.Select(MapToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    // -------------------------------------------------------------------------
    // Status transitions. Cancellation restores stock exactly once: the status
    // check and the stock restore happen inside one transaction, and the
    // rowversion token on Order makes concurrent updates fail with 409 instead
    // of double-restoring stock.
    // -------------------------------------------------------------------------
    public async Task<OrderDto> UpdateStatusAsync(int orderId, ClaimsPrincipal principal, string newStatus)
    {
        var userId = principal.GetUserId();
        var isAdmin = principal.IsInRole(UserRoles.Admin);

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var order = await _db.Orders
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order is null)
        {
            throw new NotFoundException($"Order {orderId} was not found.");
        }

        if (!isAdmin && order.CustomerId != userId)
        {
            throw new ForbiddenException("You do not have access to this order.");
        }

        // Customers may only cancel their own orders; every other transition is admin-only.
        if (!isAdmin && newStatus != OrderStatus.Cancelled)
        {
            throw new ForbiddenException("Customers may only cancel their own orders.");
        }

        if (!OrderStatusTransitions.CanTransition(order.Status, newStatus))
        {
            throw new OrderValidationException(
                $"Order status cannot change from '{order.Status}' to '{newStatus}'. " +
                OrderStatusTransitions.AllowedTransitionsDescription(order.Status));
        }

        if (newStatus == OrderStatus.Cancelled)
        {
            foreach (var item in order.Items)
            {
                item.Product!.StockQuantity += item.Quantity; // restore stock exactly once
            }
        }

        var originalStatus = order.Status;
        order.Status = newStatus;
        order.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "The order was modified by another request (it may have been cancelled or updated concurrently). " +
                "Please refresh and try again.");
        }

        await transaction.CommitAsync();
        _logger.LogInformation("Order {OrderId} status changed from {From} to {To} by user {UserId}.",
            orderId, originalStatus, newStatus, userId);

        return MapToDto(order);
    }

    private static void EnsureCanAccess(Order order, ClaimsPrincipal principal)
    {
        if (principal.IsInRole(UserRoles.Admin))
        {
            return;
        }

        var userId = principal.GetUserId();
        if (order.CustomerId != userId)
        {
            throw new ForbiddenException("You do not have access to this order.");
        }
    }

    private static OrderDto MapToDto(Order order) => new()
    {
        Id = order.Id,
        CustomerId = order.CustomerId,
        CustomerName = order.Customer?.FullName ?? string.Empty,
        Status = order.Status,
        SubTotal = order.SubTotal,
        Discount = order.Discount,
        Total = order.Total,
        CreatedAt = order.CreatedAt,
        UpdatedAt = order.UpdatedAt,
        Items = order.Items.Select(i => new OrderItemDto
        {
            ProductId = i.ProductId,
            ProductName = i.Product?.Name ?? string.Empty,
            Sku = i.Product?.Sku ?? string.Empty,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            LineTotal = i.LineTotal
        }).ToList()
    };
}
