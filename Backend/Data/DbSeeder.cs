using Backend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

/// <summary>
/// Seeds demo data. DEVELOPMENT CREDENTIALS ONLY - they are public in this
/// repository and MUST be changed before any real deployment.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IPasswordHasher<User> hasher)
    {
        if (await db.Users.AnyAsync())
        {
            return; // Already seeded.
        }

        var users = new List<User>
        {
            new() { FullName = "System Administrator", Email = "admin@orderinventory.com", Role = UserRoles.Admin },
            new() { FullName = "Alice Johnson", Email = "alice@example.com", Role = UserRoles.Customer },
            new() { FullName = "Bob Smith", Email = "bob@example.com", Role = UserRoles.Customer },
            new() { FullName = "Carol Williams", Email = "carol@example.com", Role = UserRoles.Customer }
        };

        var passwords = new[] { "Admin@123", "Customer1@123", "Customer2@123", "Customer3@123" };

        for (var i = 0; i < users.Count; i++)
        {
            users[i].PasswordHash = hasher.HashPassword(users[i], passwords[i]);
            db.Users.Add(users[i]);
        }

        var products = new List<Product>
        {
            new() { Sku = "SKU-001", Name = "Wireless Mouse", Description = "Ergonomic 2.4 GHz wireless mouse with USB receiver.", Price = 24.99m, StockQuantity = 120 },
            new() { Sku = "SKU-002", Name = "Mechanical Keyboard", Description = "87-key mechanical keyboard with blue switches.", Price = 89.50m, StockQuantity = 45 },
            new() { Sku = "SKU-003", Name = "27-inch 4K Monitor", Description = "27-inch IPS 4K UHD monitor with HDMI and DisplayPort.", Price = 329.99m, StockQuantity = 18 },
            new() { Sku = "SKU-004", Name = "USB-C Hub", Description = "7-in-1 USB-C hub with HDMI, Ethernet and card reader.", Price = 39.99m, StockQuantity = 80 },
            new() { Sku = "SKU-005", Name = "Laptop Stand", Description = "Adjustable aluminum laptop stand for 10-17 inch laptops.", Price = 54.25m, StockQuantity = 60 },
            new() { Sku = "SKU-006", Name = "Noise-Cancelling Headphones", Description = "Over-ear Bluetooth headphones with active noise cancellation.", Price = 199.00m, StockQuantity = 25 },
            new() { Sku = "SKU-007", Name = "1080p Webcam", Description = "Full HD webcam with built-in microphone and privacy cover.", Price = 59.99m, StockQuantity = 0 },
            new() { Sku = "SKU-008", Name = "Standing Desk Converter", Description = "Height-adjustable desk converter for dual-monitor setups.", Price = 249.99m, StockQuantity = 10 },
            new() { Sku = "SKU-009", Name = "Ergonomic Office Chair", Description = "Mesh-back office chair with adjustable lumbar support.", Price = 459.00m, StockQuantity = 7 },
            new() { Sku = "SKU-010", Name = "LED Desk Lamp", Description = "Dimmable LED desk lamp with 5 color temperatures.", Price = 29.99m, StockQuantity = 95 },
            new() { Sku = "SKU-011", Name = "Monitor Arm", Description = "Full-motion gas spring monitor arm for screens up to 32 inch.", Price = 74.50m, StockQuantity = 33 },
            new() { Sku = "SKU-012", Name = "HDMI Cable 2m", Description = "High-speed HDMI 2.1 cable, 2 meters, supports 8K@60Hz.", Price = 12.99m, StockQuantity = 200 },
            new() { Sku = "SKU-013", Name = "Portable SSD 1TB", Description = "USB 3.2 Gen 2 portable SSD, 1TB, 1050 MB/s read.", Price = 109.99m, StockQuantity = 40 },
            new() { Sku = "SKU-014", Name = "Wireless Presenter", Description = "RF wireless presenter remote with red laser pointer.", Price = 34.99m, StockQuantity = 55 },
            new() { Sku = "SKU-015", Name = "Laptop Backpack", Description = "Water-resistant 15.6-inch laptop backpack with USB port.", Price = 79.99m, StockQuantity = 3 }
        };

        db.Products.AddRange(products);
        await db.SaveChangesAsync();
    }
}
