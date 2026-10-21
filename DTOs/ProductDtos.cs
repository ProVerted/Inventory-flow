using System.ComponentModel.DataAnnotations;

namespace InventorySystem.DTOs
{
    public class CreateProductRequest
    {
        [Required(ErrorMessage = "Product name is required.")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "Product name must be between 2 and 150 characters.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "SKU is required.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "SKU must be between 2 and 50 characters.")]
        public string SKU { get; set; } = string.Empty;

        [Range(0.01, 1000000.00, ErrorMessage = "Price must be greater than 0.")]
        public decimal Price { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Stock quantity cannot be negative.")]
        public int StockQuantity { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Low stock alert threshold cannot be negative.")]
        public int LowStockAlert { get; set; } = 5;
    }

    public class UpdateProductRequest
    {
        [Required(ErrorMessage = "Product name is required.")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "Product name must be between 2 and 150 characters.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "SKU is required.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "SKU must be between 2 and 50 characters.")]
        public string SKU { get; set; } = string.Empty;

        [Range(0.01, 1000000.00, ErrorMessage = "Price must be greater than 0.")]
        public decimal Price { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Stock quantity cannot be negative.")]
        public int StockQuantity { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Low stock alert threshold cannot be negative.")]
        public int LowStockAlert { get; set; }
    }

    public class AdjustStockRequest
    {
        [Required]
        public int AdjustmentQuantity { get; set; }

        public string? Reason { get; set; }
    }

    public class ProductResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public int LowStockAlert { get; set; }
        public bool IsLowStock => StockQuantity <= LowStockAlert;
    }
}
