namespace InventorySystem.Models;
public class Invoice
{
    public int Id {get;set;}
    public string InvoiceNumber {get;set;}=string.Empty;
    public decimal TotalAmount {get;set;}
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<InvoiceItem> Items { get; set; } = new();
}