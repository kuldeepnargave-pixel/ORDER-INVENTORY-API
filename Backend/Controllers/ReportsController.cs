using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = UserRoles.Admin)]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService) => _reportService = reportService;

    /// <summary>
    /// Top customers by total spending on qualifying orders (Confirmed + Shipped).
    /// Executes as a single SQL GROUP BY query; the generated SQL is logged in
    /// development (see README "Generated SQL logging").
    /// </summary>
    [HttpGet("top-customers")]
    public async Task<ActionResult<List<TopCustomerDto>>> GetTopCustomers([FromQuery] int top = 5)
    {
        if (top < 1 || top > 100) top = 5;
        return Ok(await _reportService.GetTopCustomersAsync(top));
    }
}
