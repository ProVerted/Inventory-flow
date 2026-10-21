using System.ComponentModel.DataAnnotations;

namespace InventorySystem.DTOs
{
    public class CreateInvoiceItemRequest
    {
        [Required(ErrorMessage = "ProductId is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "A valid ProductId is required.")]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Quantity is required.")]
        [Range(1, 100000, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; }
    }

    public class CreateInvoiceRequest
    {
        [Required]
        [MinLength(1, ErrorMessage = "Invoice must contain at least one item.")]
        public List<CreateInvoiceItemRequest> Items { get; set; } = new();
    }

    public class InvoiceItemResponse
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductSKU { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal SubTotal => Quantity * UnitPrice;
    }

    public class InvoiceResponse
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int CreatedByUserId { get; set; }
        public string CreatedByUsername { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<InvoiceItemResponse> Items { get; set; } = new();
    }

    public class InvoiceSummaryResponse
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int CreatedByUserId { get; set; }
        public string CreatedByUsername { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int TotalItemsCount { get; set; }
    }
}
