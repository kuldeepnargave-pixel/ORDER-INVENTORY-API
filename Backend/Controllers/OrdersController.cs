using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Backend.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService) => _orderService = orderService;

    /// <summary>
    /// Create an order. Requires the Idempotency-Key header. Prices and totals
    /// are always derived from the database; the first successful request returns
    /// HTTP 201, a replay of the same key+payload returns HTTP 200 with the
    /// existing order, and a replay with a different payload returns HTTP 409.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<OrderDto>> CreateOrder([FromBody] OrderCreateRequest request)
    {
        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Missing required header",
                Detail = "The 'Idempotency-Key' header is required for order creation.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var result = await _orderService.CreateOrderAsync(User.GetUserId(), request, idempotencyKey.Trim());

        return result.Created
            ? CreatedAtAction(nameof(GetOrder), new { id = result.Order.Id }, result.Order)
            : Ok(result.Order);
    }

    /// <summary>List the signed-in customer's own orders.</summary>
    [HttpGet("my")]
    public async Task<ActionResult<List<OrderDto>>> GetMyOrders()
    {
        return Ok(await _orderService.GetMyOrdersAsync(User.GetUserId()));
    }

    /// <summary>List all orders (paged). Admin only.</summary>
    [HttpGet]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<ActionResult<PagedResult<OrderDto>>> GetAllOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;
        return Ok(await _orderService.GetAllOrdersAsync(page, pageSize));
    }

    /// <summary>Get one order. Admin: any order. Customer: own orders only (else HTTP 403).</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> GetOrder(int id)
    {
        return Ok(await _orderService.GetOrderAsync(id, User));
    }

    /// <summary>
    /// Change order status. Admin: any allowed transition.
    /// Customer: may only cancel their own Pending/Confirmed orders.
    /// Cancellation restores stock exactly once.
    /// </summary>
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<OrderDto>> UpdateStatus(int id, OrderStatusUpdateRequest request)
    {
        return Ok(await _orderService.UpdateStatusAsync(id, User, request.Status.Trim()));
    }
}
