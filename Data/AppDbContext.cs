using Microsoft.EntityFrameworkCore;
using InventorySystem.Models;

namespace InventorySystem.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) {}

        public DbSet<User> Users { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceItem> InvoiceItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Product>(entity =>
            {
                entity.Property(p => p.Price).HasPrecision(18, 2);
            });

            modelBuilder.Entity<Invoice>(entity =>
            {
                entity.Property(i => i.TotalAmount).HasPrecision(18, 2);
            });

            modelBuilder.Entity<InvoiceItem>(entity =>
            {
                entity.Property(ii => ii.Price).HasPrecision(18, 2);
            });
        }
    }
}