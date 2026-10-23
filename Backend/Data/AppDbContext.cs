using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SQL Server uses real rowversion columns (DB-generated, fully
        // automatic optimistic concurrency). The SQLite provider (used only by
        // the unit tests) has no rowversion support, so there the RowVersion
        // property is mapped as a plain application-managed concurrency token
        // instead; the unit tests emulate rowversion regeneration with a
        // SaveChanges interceptor. SQL Server behaviour is unchanged.
        var useSqlServerRowVersion = Database.ProviderName != "Microsoft.EntityFrameworkCore.Sqlite";

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.FullName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(256).IsRequired();
            entity.Property(u => u.Role).HasMaxLength(20).IsRequired();
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(p => p.Sku).HasMaxLength(50).IsRequired();
            entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Description).HasMaxLength(1000);
            entity.Property(p => p.Price).HasPrecision(18, 2);
            if (useSqlServerRowVersion)
            {
                entity.Property(p => p.RowVersion).IsRowVersion();
            }
            else
            {
                entity.Property(p => p.RowVersion).IsConcurrencyToken();
            }
            entity.HasIndex(p => p.Sku).IsUnique();
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Products_Price_NonNegative", "[Price] >= 0");
                t.HasCheckConstraint("CK_Products_Stock_NonNegative", "[StockQuantity] >= 0");
            });
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.Property(o => o.Status).HasMaxLength(20).IsRequired();
            entity.Property(o => o.SubTotal).HasPrecision(18, 2);
            entity.Property(o => o.Discount).HasPrecision(18, 2);
            entity.Property(o => o.Total).HasPrecision(18, 2);
            entity.Property(o => o.IdempotencyKey).HasMaxLength(128).IsRequired();
            entity.Property(o => o.IdempotencyPayloadHash).HasMaxLength(64).IsRequired();
            if (useSqlServerRowVersion)
            {
                entity.Property(o => o.RowVersion).IsRowVersion();
            }
            else
            {
                entity.Property(o => o.RowVersion).IsConcurrencyToken();
            }

            // Enforces idempotency uniqueness per customer at the database level.
            entity.HasIndex(o => new { o.CustomerId, o.IdempotencyKey })
                  .IsUnique()
                  .HasDatabaseName("UX_Orders_Customer_IdempotencyKey");

            entity.HasOne(o => o.Customer)
                  .WithMany(u => u.Orders)
                  .HasForeignKey(o => o.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Orders_SubTotal_NonNegative", "[SubTotal] >= 0");
                t.HasCheckConstraint("CK_Orders_Discount_NonNegative", "[Discount] >= 0");
                t.HasCheckConstraint("CK_Orders_Total_NonNegative", "[Total] >= 0");
            });
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.Property(i => i.UnitPrice).HasPrecision(18, 2);
            entity.Property(i => i.LineTotal).HasPrecision(18, 2);

            entity.HasOne(i => i.Order)
                  .WithMany(o => o.Items)
                  .HasForeignKey(i => i.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(i => i.Product)
                  .WithMany()
                  .HasForeignKey(i => i.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(i => i.ProductId);
            entity.ToTable(t => t.HasCheckConstraint("CK_OrderItems_Quantity_Positive", "[Quantity] > 0"));
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(t => t.TokenHash).IsUnique();
            entity.HasOne(t => t.User)
                  .WithMany(u => u.RefreshTokens)
                  .HasForeignKey(t => t.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
