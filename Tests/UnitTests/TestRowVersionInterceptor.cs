using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace UnitTests;

/// <summary>
/// TEST-ONLY interceptor that emulates SQL Server's automatic rowversion
/// generation, which the SQLite provider (used by the unit tests for real
/// relational semantics) does not implement:
///  - INSERT: fills the empty RowVersion token with random bytes (SQL Server
///    generates the rowversion in the database).
///  - UPDATE: assigns a fresh value to the RowVersion token, exactly like a
///    SQL Server rowversion column changes on every update.
/// This makes optimistic-concurrency tests behave like they would against
/// SQL Server. The production backend and the integration tests use real
/// SQL Server rowversion and do not rely on this interceptor.
/// </summary>
public class TestRowVersionInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        FillRowVersions(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        FillRowVersions(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void FillRowVersions(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added && entry.State != EntityState.Modified)
            {
                continue;
            }

            var rowVersion = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "RowVersion");
            if (rowVersion is null || rowVersion.Metadata.ClrType != typeof(byte[]))
            {
                continue;
            }

            if (entry.State == EntityState.Modified
                || rowVersion.CurrentValue is null
                || ((byte[])rowVersion.CurrentValue).Length == 0)
            {
                rowVersion.CurrentValue = Guid.NewGuid().ToByteArray();
            }
        }
    }
}
