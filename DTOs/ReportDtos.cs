namespace InventorySystem.DTOs
{
    public class DashboardReport
    {
        public int TotalProducts { get; set; }
        public int TotalStockQuantity { get; set; }
        public decimal TotalInventoryValuation { get; set; }
        public int LowStockProductsCount { get; set; }
        public int TotalInvoicesCount { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class TopSellingProduct
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public int TotalQuantitySold { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class DailySalesSummary
    {
        public DateTime Date { get; set; }
        public int InvoiceCount { get; set; }
        public decimal DailyRevenue { get; set; }
    }

    public class SalesReportResponse
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int TotalInvoices { get; set; }
        public decimal TotalRevenue { get; set; }
        public List<DailySalesSummary> DailySales { get; set; } = new();
        public List<TopSellingProduct> TopSellingProducts { get; set; } = new();
    }
}
