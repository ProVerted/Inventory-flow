using InventorySystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Data
{
    public static class DbInitializer
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

            try
            {
                // Verify database connectivity
                if (!await context.Database.CanConnectAsync())
                {
                    logger.LogWarning("Database cannot be reached currently. Skipping seeding.");
                    return;
                }

                // 1. Seed initial Admin user if table is empty
                if (!await context.Users.AnyAsync())
                {
                    logger.LogInformation("Seeding default Administrator user...");
                    var admin = new User
                    {
                        Username = "admin",
                        Role = "Admin"
                    };
                    admin.PasswordHash = passwordHasher.HashPassword(admin, "Admin123!");

                    var staff = new User
                    {
                        Username = "staff",
                        Role = "Staff"
                    };
                    staff.PasswordHash = passwordHasher.HashPassword(staff, "Staff123!");

                    context.Users.AddRange(admin, staff);
                    await context.SaveChangesAsync();
                    logger.LogInformation("Admin ('admin' / 'Admin123!') and Staff ('staff' / 'Staff123!') seeded successfully.");
                }

                // 2. Seed initial catalog if Products table is empty
                if (!await context.Products.AnyAsync())
                {
                    logger.LogInformation("Seeding sample product catalog...");
                    var products = new List<Product>
                    {
                        new() { Name = "Wireless Ergonomic Mouse", SKU = "PER-MOU-01", Price = 29.99m, StockQuantity = 45, LowStockAlert = 10 },
                        new() { Name = "Mechanical Keyboard RGB", SKU = "PER-KBD-02", Price = 89.99m, StockQuantity = 20, LowStockAlert = 5 },
                        new() { Name = "27-inch 4K UHD Monitor", SKU = "MON-4K-03", Price = 349.50m, StockQuantity = 8, LowStockAlert = 3 },
                        new() { Name = "USB-C Multiport Hub", SKU = "ACC-HUB-04", Price = 42.00m, StockQuantity = 2, LowStockAlert = 5 }, // Low stock sample
                        new() { Name = "Noise Cancelling Headphones", SKU = "AUD-NCH-05", Price = 129.99m, StockQuantity = 15, LowStockAlert = 5 }
                    };

                    context.Products.AddRange(products);
                    await context.SaveChangesAsync();
                    logger.LogInformation("Sample products seeded successfully.");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "An error occurred while seeding the database: {Message}", ex.Message);
            }
        }
    }
}
