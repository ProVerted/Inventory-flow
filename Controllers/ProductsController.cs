using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.DTOs;
using InventorySystem.Models;

namespace InventorySystem.Controllers
{
    [ApiController]
    [Route("products")]
    [Authorize]
    public class ProductsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProductsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /products?search=item&lowStockOnly=false
        [HttpGet]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<ActionResult<IEnumerable<ProductResponse>>> GetProducts([FromQuery] string? search, [FromQuery] bool? lowStockOnly)
        {
            var query = _context.Products.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(term) || p.SKU.ToLower().Contains(term));
            }

            if (lowStockOnly == true)
            {
                query = query.Where(p => p.StockQuantity <= p.LowStockAlert);
            }

            var products = await query
                .OrderBy(p => p.Name)
                .Select(p => new ProductResponse
                {
                    Id = p.Id,
                    Name = p.Name,
                    SKU = p.SKU,
                    Price = p.Price,
                    StockQuantity = p.StockQuantity,
                    LowStockAlert = p.LowStockAlert
                })
                .ToListAsync();

            return Ok(products);
        }

        // GET: /products/low-stock
        [HttpGet("low-stock")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<ActionResult<IEnumerable<ProductResponse>>> GetLowStockProducts()
        {
            var lowStockProducts = await _context.Products
                .AsNoTracking()
                .Where(p => p.StockQuantity <= p.LowStockAlert)
                .OrderBy(p => p.StockQuantity)
                .Select(p => new ProductResponse
                {
                    Id = p.Id,
                    Name = p.Name,
                    SKU = p.SKU,
                    Price = p.Price,
                    StockQuantity = p.StockQuantity,
                    LowStockAlert = p.LowStockAlert
                })
                .ToListAsync();

            return Ok(lowStockProducts);
        }

        // GET: /products/{id}
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<ActionResult<ProductResponse>> GetProductById(int id)
        {
            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound(new { message = $"Product with ID {id} not found." });
            }

            return Ok(new ProductResponse
            {
                Id = product.Id,
                Name = product.Name,
                SKU = product.SKU,
                Price = product.Price,
                StockQuantity = product.StockQuantity,
                LowStockAlert = product.LowStockAlert
            });
        }

        // POST: /products
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ProductResponse>> AddProduct([FromBody] CreateProductRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var normalizedSku = request.SKU.Trim().ToUpper();
            var skuExists = await _context.Products.AnyAsync(p => p.SKU.ToUpper() == normalizedSku);
            if (skuExists)
            {
                return Conflict(new { message = $"A product with SKU '{request.SKU}' already exists." });
            }

            var product = new Product
            {
                Name = request.Name.Trim(),
                SKU = normalizedSku,
                Price = request.Price,
                StockQuantity = request.StockQuantity,
                LowStockAlert = request.LowStockAlert
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            var response = new ProductResponse
            {
                Id = product.Id,
                Name = product.Name,
                SKU = product.SKU,
                Price = product.Price,
                StockQuantity = product.StockQuantity,
                LowStockAlert = product.LowStockAlert
            };

            return CreatedAtAction(nameof(GetProductById), new { id = product.Id }, response);
        }

        // PUT: /products/{id}
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ProductResponse>> UpdateProduct(int id, [FromBody] UpdateProductRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound(new { message = $"Product with ID {id} not found." });
            }

            var normalizedSku = request.SKU.Trim().ToUpper();
            var skuConflict = await _context.Products.AnyAsync(p => p.Id != id && p.SKU.ToUpper() == normalizedSku);
            if (skuConflict)
            {
                return Conflict(new { message = $"Another product with SKU '{request.SKU}' already exists." });
            }

            product.Name = request.Name.Trim();
            product.SKU = normalizedSku;
            product.Price = request.Price;
            product.StockQuantity = request.StockQuantity;
            product.LowStockAlert = request.LowStockAlert;

            await _context.SaveChangesAsync();

            return Ok(new ProductResponse
            {
                Id = product.Id,
                Name = product.Name,
                SKU = product.SKU,
                Price = product.Price,
                StockQuantity = product.StockQuantity,
                LowStockAlert = product.LowStockAlert
            });
        }

        // PATCH: /products/{id}/stock
        [HttpPatch("{id:int}/stock")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ProductResponse>> AdjustStock(int id, [FromBody] AdjustStockRequest request)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound(new { message = $"Product with ID {id} not found." });
            }

            var updatedStock = product.StockQuantity + request.AdjustmentQuantity;
            if (updatedStock < 0)
            {
                return BadRequest(new
                {
                    message = $"Cannot adjust stock by {request.AdjustmentQuantity}. Current stock is {product.StockQuantity}, resulting quantity cannot be negative."
                });
            }

            product.StockQuantity = updatedStock;
            await _context.SaveChangesAsync();

            return Ok(new ProductResponse
            {
                Id = product.Id,
                Name = product.Name,
                SKU = product.SKU,
                Price = product.Price,
                StockQuantity = product.StockQuantity,
                LowStockAlert = product.LowStockAlert
            });
        }

        // DELETE: /products/{id}
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound(new { message = $"Product with ID {id} not found." });
            }

            var isUsedInInvoices = await _context.InvoiceItems.AnyAsync(ii => ii.ProductId == id);
            if (isUsedInInvoices)
            {
                return BadRequest(new
                {
                    message = "Cannot delete this product because it is referenced in existing invoice records. Consider adjusting its stock to 0 instead."
                });
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Product '{product.Name}' (ID: {id}) deleted successfully." });
        }
    }
}