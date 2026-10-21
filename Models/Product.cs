namespace InventorySystem.Models;

public class Product
{
    public int Id {get;set;}
    public string Name {get;set;}= string.Empty;
    public string SKU {get;set;}= string.Empty;
    public decimal Price {get;set;}
    public int StockQuantity {get;set;}
    public int LowStockAlert {get;set;}

}