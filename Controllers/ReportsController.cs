using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs;

namespace InventorySystem.Controllers
{
    [ApiController]
    [Route("reports")]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ReportsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /reports/dashboard
        [HttpGet("dashboard")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<ActionResult<DashboardReport>> GetDashboardMetrics()
        {
            var totalProducts = await _context.Products.CountAsync();
            var totalStockQuantity = await _context.Products.SumAsync(p => (int?)p.StockQuantity) ?? 0;
            var totalValuation = await _context.Products.SumAsync(p => (decimal?)(p.Price * p.StockQuantity)) ?? 0m;
            var lowStockCount = await _context.Products.CountAsync(p => p.StockQuantity <= p.LowStockAlert);
            var totalInvoices = await _context.Invoices.CountAsync();
            var totalRevenue = await _context.Invoices.SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;

            return Ok(new DashboardReport
            {
                TotalProducts = totalProducts,
                TotalStockQuantity = totalStockQuantity,
                TotalInventoryValuation = totalValuation,
                LowStockProductsCount = lowStockCount,
                TotalInvoicesCount = totalInvoices,
                TotalRevenue = totalRevenue
            });
        }

        // GET: /reports/sales?startDate=2026-01-01&endDate=2026-12-31
        [HttpGet("sales")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<SalesReportResponse>> GetSalesReport(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            var invoiceQuery = _context.Invoices.AsNoTracking().AsQueryable();

            if (startDate.HasValue)
            {
                invoiceQuery = invoiceQuery.Where(i => i.CreatedAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                invoiceQuery = invoiceQuery.Where(i => i.CreatedAt <= endDate.Value);
            }

            var invoices = await invoiceQuery.ToListAsync();
            var invoiceIds = invoices.Select(i => i.Id).ToList();

            var totalRevenue = invoices.Sum(i => i.TotalAmount);
            var totalInvoices = invoices.Count;

            // Daily sales breakdown
            var dailySales = invoices
                .GroupBy(i => i.CreatedAt.Date)
                .OrderBy(g => g.Key)
                .Select(g => new DailySalesSummary
                {
                    Date = g.Key,
                    InvoiceCount = g.Count(),
                    DailyRevenue = g.Sum(x => x.TotalAmount)
                })
                .ToList();

            // Top selling products
            var items = await _context.InvoiceItems
                .AsNoTracking()
                .Where(ii => invoiceIds.Contains(ii.InvoiceId))
                .ToListAsync();

            var itemProductIds = items.Select(ii => ii.ProductId).Distinct().ToList();
            var productLookup = await _context.Products
                .AsNoTracking()
                .Where(p => itemProductIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => new { p.Name, p.SKU });

            var topSellingProducts = items
                .GroupBy(ii => ii.ProductId)
                .Select(g =>
                {
                    var hasProduct = productLookup.TryGetValue(g.Key, out var prod);
                    return new TopSellingProduct
                    {
                        ProductId = g.Key,
                        ProductName = hasProduct ? prod!.Name : "Archived Product",
                        SKU = hasProduct ? prod!.SKU : "N/A",
                        TotalQuantitySold = g.Sum(x => x.Quantity),
                        TotalRevenue = g.Sum(x => x.Price * x.Quantity)
                    };
                })
                .OrderByDescending(p => p.TotalQuantitySold)
                .Take(10)
                .ToList();

            return Ok(new SalesReportResponse
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalInvoices = totalInvoices,
                TotalRevenue = totalRevenue,
                DailySales = dailySales,
                TopSellingProducts = topSellingProducts
            });
        }
    }
}
