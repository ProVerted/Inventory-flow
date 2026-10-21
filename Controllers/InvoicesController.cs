using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using InventorySystem.Data;
using InventorySystem.DTOs;
using InventorySystem.Models;

namespace InventorySystem.Controllers
{
    [ApiController]
    [Route("invoices")]
    [Authorize(Roles = "Admin,Staff")]
    public class InvoicesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public InvoicesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /invoices?startDate=2026-01-01&endDate=2026-12-31
        [HttpGet]
        public async Task<ActionResult<IEnumerable<InvoiceSummaryResponse>>> GetInvoices(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            var query = _context.Invoices.AsNoTracking().AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(i => i.CreatedAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(i => i.CreatedAt <= endDate.Value);
            }

            var invoices = await query
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => new
                {
                    i.Id,
                    i.InvoiceNumber,
                    i.TotalAmount,
                    i.CreatedBy,
                    i.CreatedAt,
                    TotalItemsCount = i.Items.Sum(item => item.Quantity)
                })
                .ToListAsync();

            var userIds = invoices.Select(i => i.CreatedBy).Distinct().ToList();
            var usernames = await _context.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Username);

            var result = invoices.Select(i => new InvoiceSummaryResponse
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                TotalAmount = i.TotalAmount,
                CreatedByUserId = i.CreatedBy,
                CreatedByUsername = usernames.TryGetValue(i.CreatedBy, out var name) ? name : "Unknown",
                CreatedAt = i.CreatedAt,
                TotalItemsCount = i.TotalItemsCount
            }).ToList();

            return Ok(result);
        }

        // GET: /invoices/{id}
        [HttpGet("{id:int}")]
        public async Task<ActionResult<InvoiceResponse>> GetInvoiceById(int id)
        {
            var invoice = await _context.Invoices
                .AsNoTracking()
                .Include(i => i.Items)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null)
            {
                return NotFound(new { message = $"Invoice with ID {id} not found." });
            }

            var productIds = invoice.Items.Select(item => item.ProductId).Distinct().ToList();
            var products = await _context.Products
                .AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => new { p.Name, p.SKU });

            var creator = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == invoice.CreatedBy);

            var response = new InvoiceResponse
            {
                Id = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                TotalAmount = invoice.TotalAmount,
                CreatedByUserId = invoice.CreatedBy,
                CreatedByUsername = creator?.Username ?? "Unknown",
                CreatedAt = invoice.CreatedAt,
                Items = invoice.Items.Select(item => new InvoiceItemResponse
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    ProductName = products.TryGetValue(item.ProductId, out var prod) ? prod.Name : "Product no longer available",
                    ProductSKU = products.TryGetValue(item.ProductId, out prod) ? prod.SKU : "N/A",
                    Quantity = item.Quantity,
                    UnitPrice = item.Price
                }).ToList()
            };

            return Ok(response);
        }

        // POST: /invoices
        [HttpPost]
        public async Task<ActionResult<InvoiceResponse>> CreateInvoice([FromBody] CreateInvoiceRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request.Items == null || request.Items.Count == 0)
            {
                return BadRequest(new { message = "An invoice must contain at least one line item." });
            }

            // Aggregate duplicate products in the request
            var aggregatedRequests = request.Items
                .GroupBy(i => i.ProductId)
                .Select(g => new { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
                .ToList();

            // Resolve authenticated user ID
            int currentUserId = 0;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim))
            {
                int.TryParse(userIdClaim, out currentUserId);
            }

            if (currentUserId == 0)
            {
                var username = User.Identity?.Name;
                if (!string.IsNullOrEmpty(username))
                {
                    var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
                    currentUserId = user?.Id ?? 0;
                }
            }

            // Begin atomic transaction
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var requestedProductIds = aggregatedRequests.Select(r => r.ProductId).ToList();
                var products = await _context.Products
                    .Where(p => requestedProductIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id);

                // 1. Validate existence of all requested products
                foreach (var req in aggregatedRequests)
                {
                    if (!products.ContainsKey(req.ProductId))
                    {
                        return BadRequest(new { message = $"Product with ID {req.ProductId} does not exist." });
                    }
                }

                // 2. Validate stock availability
                foreach (var req in aggregatedRequests)
                {
                    var product = products[req.ProductId];
                    if (product.StockQuantity < req.Quantity)
                    {
                        return BadRequest(new
                        {
                            message = $"Insufficient stock for product '{product.Name}' (SKU: {product.SKU}). Available stock: {product.StockQuantity}, Requested: {req.Quantity}."
                        });
                    }
                }

                // 3. Deduct stock and assemble invoice line items
                var invoiceItems = new List<InvoiceItem>();
                decimal totalAmount = 0m;

                foreach (var req in aggregatedRequests)
                {
                    var product = products[req.ProductId];

                    // Deduct stock
                    product.StockQuantity -= req.Quantity;

                    var lineTotal = product.Price * req.Quantity;
                    totalAmount += lineTotal;

                    invoiceItems.Add(new InvoiceItem
                    {
                        ProductId = product.Id,
                        Quantity = req.Quantity,
                        Price = product.Price // Locked-in historical price
                    });
                }

                // 4. Generate unique invoice number
                var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                var randomSuffix = Random.Shared.Next(100, 999);
                var invoiceNumber = $"INV-{timestamp}-{randomSuffix}";

                var invoice = new Invoice
                {
                    InvoiceNumber = invoiceNumber,
                    TotalAmount = totalAmount,
                    CreatedBy = currentUserId,
                    CreatedAt = DateTime.UtcNow,
                    Items = invoiceItems
                };

                _context.Invoices.Add(invoice);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                // Format response
                var creator = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUserId);

                var response = new InvoiceResponse
                {
                    Id = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber,
                    TotalAmount = invoice.TotalAmount,
                    CreatedByUserId = currentUserId,
                    CreatedByUsername = creator?.Username ?? "Unknown",
                    CreatedAt = invoice.CreatedAt,
                    Items = invoice.Items.Select(item =>
                    {
                        var prod = products[item.ProductId];
                        return new InvoiceItemResponse
                        {
                            Id = item.Id,
                            ProductId = item.ProductId,
                            ProductName = prod.Name,
                            ProductSKU = prod.SKU,
                            Quantity = item.Quantity,
                            UnitPrice = item.Price
                        };
                    }).ToList()
                };

                return CreatedAtAction(nameof(GetInvoiceById), new { id = invoice.Id }, response);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "An error occurred while processing the invoice transaction.",
                    detail = ex.Message
                });
            }
        }
    }
}