using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InvoiceGen.Data;
using InvoiceGen.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGen.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PurchaseOrdersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PurchaseOrdersController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PurchaseOrder>>> GetPurchaseOrders([FromQuery] int? userId)
        {
            var query = _context.PurchaseOrders.Include(p => p.Client).Include(p => p.Items).AsQueryable();
            if (userId.HasValue)
            {
                query = query.Where(p => p.UserId == userId.Value);
            }
            return await query.OrderByDescending(p => p.Id).ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<PurchaseOrder>> PostPurchaseOrder(PurchaseOrder purchaseOrder)
        {
            var existing = await _context.PurchaseOrders
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.PONumber == purchaseOrder.PONumber && (purchaseOrder.UserId == null || p.UserId == purchaseOrder.UserId));

            if (existing != null)
            {
                existing.Date = purchaseOrder.Date;
                existing.DueDate = purchaseOrder.DueDate;
                existing.TotalAmount = purchaseOrder.TotalAmount;
                if (existing.Status == "Approved" && !purchaseOrder.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
                {
                    // preserve Approved
                }
                else
                {
                    existing.Status = purchaseOrder.Status;
                }
                if (!string.IsNullOrEmpty(purchaseOrder.FullDataJson))
                {
                    existing.FullDataJson = purchaseOrder.FullDataJson;
                }

                if (purchaseOrder.Client != null && !string.IsNullOrEmpty(purchaseOrder.Client.Name))
                {
                    var clientName = purchaseOrder.Client.Name.Trim();
                    var dbClient = await _context.Clients.FirstOrDefaultAsync(c => c.Name.ToLower() == clientName.ToLower());
                    if (dbClient != null)
                    {
                        existing.ClientId = dbClient.Id;
                    }
                    else if (!string.IsNullOrEmpty(clientName) && clientName != "Unknown Client")
                    {
                        var newClient = new Client { Name = clientName, UserId = purchaseOrder.UserId };
                        _context.Clients.Add(newClient);
                        await _context.SaveChangesAsync();
                        existing.ClientId = newClient.Id;
                    }
                }

                if (purchaseOrder.Items != null && purchaseOrder.Items.Count > 0)
                {
                    _context.PurchaseOrderItems.RemoveRange(existing.Items);
                    foreach (var it in purchaseOrder.Items)
                    {
                        it.PurchaseOrderId = existing.Id;
                        it.Id = 0;
                        _context.PurchaseOrderItems.Add(it);
                    }
                }
                
                await _context.SaveChangesAsync();
                return Ok(existing);
            }

            if (purchaseOrder.Client != null && !string.IsNullOrEmpty(purchaseOrder.Client.Name))
            {
                var clientName = purchaseOrder.Client.Name.Trim();
                var dbClient = await _context.Clients.FirstOrDefaultAsync(c => c.Name.ToLower() == clientName.ToLower());
                if (dbClient != null)
                {
                    purchaseOrder.ClientId = dbClient.Id;
                    purchaseOrder.Client = null;
                }
                else if (clientName != "Unknown Client")
                {
                    var newClient = new Client { Name = clientName, UserId = purchaseOrder.UserId };
                    _context.Clients.Add(newClient);
                    await _context.SaveChangesAsync();
                    purchaseOrder.ClientId = newClient.Id;
                    purchaseOrder.Client = null;
                }
            }
            
            _context.PurchaseOrders.Add(purchaseOrder);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetPurchaseOrder), new { id = purchaseOrder.Id }, purchaseOrder);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PurchaseOrder>> GetPurchaseOrder(string id)
        {
            var clean = (id ?? "").Trim().ToLower();
            PurchaseOrder? purchaseOrder = null;
            if (int.TryParse(id, out int intId))
            {
                purchaseOrder = await _context.PurchaseOrders
                    .Include(p => p.Client)
                    .Include(p => p.Items)
                    .FirstOrDefaultAsync(p => p.Id == intId || p.PONumber.ToLower() == clean);
            }
            else
            {
                purchaseOrder = await _context.PurchaseOrders
                    .Include(p => p.Client)
                    .Include(p => p.Items)
                    .FirstOrDefaultAsync(p => p.PONumber.ToLower() == clean);
            }

            if (purchaseOrder == null) return NotFound();
            return purchaseOrder;
        }

        [HttpGet("bynumber/{poNumber}")]
        public async Task<ActionResult<PurchaseOrder>> GetPurchaseOrderByNumber(string poNumber)
        {
            var clean = (poNumber ?? "").Trim().ToLower();
            var purchaseOrder = await _context.PurchaseOrders
                .Include(p => p.Client)
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.PONumber.ToLower() == clean);

            if (purchaseOrder == null) return NotFound();
            return purchaseOrder;
        }

        // PATCH: api/PurchaseOrders/5 or api/PurchaseOrders/PO-001
        [HttpPatch("{id}")]
        public async Task<IActionResult> PatchPurchaseOrder(string id, [FromBody] DocumentPatchDto patch)
        {
            var clean = (id ?? "").Trim().ToLower();
            PurchaseOrder? purchaseOrder = null;
            if (int.TryParse(id, out int intId))
            {
                purchaseOrder = await _context.PurchaseOrders.FindAsync(intId);
            }
            if (purchaseOrder == null)
            {
                purchaseOrder = await _context.PurchaseOrders.FirstOrDefaultAsync(p => p.PONumber.ToLower() == clean);
            }
            if (purchaseOrder == null) return NotFound();

            if (patch.Status != null)
            {
                purchaseOrder.Status = patch.Status;
                if (!string.IsNullOrWhiteSpace(purchaseOrder.FullDataJson))
                {
                    try
                    {
                        var node = System.Text.Json.Nodes.JsonNode.Parse(purchaseOrder.FullDataJson);
                        if (node is System.Text.Json.Nodes.JsonObject obj)
                        {
                            obj["status"] = patch.Status;
                            purchaseOrder.FullDataJson = obj.ToJsonString();
                        }
                    }
                    catch { }
                }
            }

            await _context.SaveChangesAsync();
            return Ok(purchaseOrder);
        }

        // PATCH: api/PurchaseOrders/bynumber/{poNumber}
        [HttpPatch("bynumber/{poNumber}")]
        public async Task<IActionResult> PatchPurchaseOrderByNumber(string poNumber, [FromBody] DocumentPatchDto patch)
        {
            return await PatchPurchaseOrder(poNumber, patch);
        }

        // DELETE: api/PurchaseOrders/5 or api/PurchaseOrders/PO-001
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePurchaseOrder(string id)
        {
            var clean = (id ?? "").Trim().ToLower();
            PurchaseOrder? purchaseOrder = null;
            if (int.TryParse(id, out int intId))
            {
                purchaseOrder = await _context.PurchaseOrders.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == intId);
            }
            if (purchaseOrder == null)
            {
                purchaseOrder = await _context.PurchaseOrders.Include(p => p.Items).FirstOrDefaultAsync(p => p.PONumber.ToLower() == clean);
            }
            if (purchaseOrder == null) return NotFound();

            if (purchaseOrder.Status != null && (purchaseOrder.Status.Equals("Pending Approval", StringComparison.OrdinalIgnoreCase) || purchaseOrder.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest(new { error = "Cannot delete a document that is Pending Approval or Approved." });
            }

            if (purchaseOrder.Items != null && purchaseOrder.Items.Count > 0)
            {
                _context.PurchaseOrderItems.RemoveRange(purchaseOrder.Items);
            }

            _context.PurchaseOrders.Remove(purchaseOrder);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
