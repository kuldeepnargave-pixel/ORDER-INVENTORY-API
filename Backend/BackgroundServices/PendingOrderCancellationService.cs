using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.BackgroundServices;

/// <summary>
/// Runs every minute (configurable): cancels Pending orders older than 30
/// minutes (configurable) and restores their stock exactly once.
/// - Each order is processed in its own transaction.
/// - A failure on one order is logged and does not stop the loop.
/// - The Order rowversion token makes concurrent processing (this service vs.
///   a customer cancelling at the same moment) safe: the loser of the race
///   gets a concurrency exception and skips the order instead of double-
///   restoring stock.
/// </summary>
public class PendingOrderCancellationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingOrderCancellationService> _logger;
    private readonly TimeSpan _scanInterval;
    private readonly int _pendingCancellationMinutes;

    public PendingOrderCancellationService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<PendingOrderCancellationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _scanInterval = TimeSpan.FromSeconds(configuration.GetValue("Orders:AutoCancelScanIntervalSeconds", 60));
        _pendingCancellationMinutes = configuration.GetValue("Orders:PendingCancellationMinutes", 30);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Pending-order auto-cancel service started: scanning every {Interval}s for Pending orders older than {Minutes} minutes.",
            _scanInterval.TotalSeconds, _pendingCancellationMinutes);

        using var timer = new PeriodicTimer(_scanInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CancelExpiredPendingOrdersAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let one failed scan kill the service.
                _logger.LogError(ex, "Pending-order cancellation scan failed; it will be retried on the next tick.");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Pending-order auto-cancel service stopped.");
    }

    private async Task CancelExpiredPendingOrdersAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = DateTime.UtcNow.AddMinutes(-_pendingCancellationMinutes);
        var expiredIds = await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Pending && o.CreatedAt < cutoff)
            .Select(o => o.Id)
            .ToListAsync(cancellationToken);

        foreach (var orderId in expiredIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

                var order = await db.Orders
                    .Include(o => o.Items).ThenInclude(i => i.Product)
                    .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

                // Another actor (e.g. the customer) may have processed it already.
                if (order is null || order.Status != OrderStatus.Pending)
                {
                    db.ChangeTracker.Clear();
                    continue;
                }

                foreach (var item in order.Items)
                {
                    item.Product!.StockQuantity += item.Quantity;
                }

                order.Status = OrderStatus.Cancelled;
                order.UpdatedAt = DateTime.UtcNow;

                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "Auto-cancelled expired Pending order {OrderId} and restored stock for {ItemCount} item(s).",
                    order.Id, order.Items.Count);
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning(
                    "Order {OrderId} was modified concurrently while being auto-cancelled; skipped (stock was not double-restored).",
                    orderId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to auto-cancel order {OrderId}; continuing with the next order.", orderId);
            }
            finally
            {
                db.ChangeTracker.Clear();
            }
        }
    }
}
