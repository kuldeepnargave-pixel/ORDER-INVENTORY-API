using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public interface IReportService
{
    Task<List<TopCustomerDto>> GetTopCustomersAsync(int top);
}

public class ReportService : IReportService
{
    /// <summary>
    /// Order statuses that count as "qualifying" for revenue reporting.
    /// Pending orders are not yet fulfilled; Cancelled orders were never paid.
    /// QUERIES.sql uses the same set so the SQL and the API agree.
    /// </summary>
    public static readonly string[] QualifyingStatuses = { OrderStatus.Confirmed, OrderStatus.Shipped };

    private readonly AppDbContext _db;
    private readonly ILogger<ReportService> _logger;

    public ReportService(AppDbContext db, ILogger<ReportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<TopCustomerDto>> GetTopCustomersAsync(int top)
    {
        // Fully SQL-translatable: one JOIN + GROUP BY + ORDER BY + TOP query.
        var query = _db.Orders.AsNoTracking()
            .Where(o => QualifyingStatuses.Contains(o.Status))
            .GroupBy(o => new { o.CustomerId, Name = o.Customer!.FullName, o.Customer.Email })
            .Select(g => new TopCustomerDto
            {
                CustomerId = g.Key.CustomerId,
                CustomerName = g.Key.Name,
                Email = g.Key.Email,
                OrderCount = g.Count(),
                TotalSpent = g.Sum(o => o.Total)
            })
            .OrderByDescending(c => c.TotalSpent)
            .Take(top);

        // Capture the generated SQL in the logs (development aid; see README).
        _logger.LogInformation("Top-customers report SQL: {Sql}", query.ToQueryString());

        return await query.ToListAsync();
    }
}
