using Backend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(AppDbContext db, IPasswordHasher<User> hasher)
    {
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(db, hasher);
    }
}
