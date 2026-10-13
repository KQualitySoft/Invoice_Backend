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
    public class InvoicesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public InvoicesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Invoices/byclient/{clientName}
        [HttpGet("byclient/{clientName}")]
        public async Task<ActionResult<IEnumerable<Invoice>>> GetInvoicesByClient(string clientName, [FromQuery] int? userId)
        {
            var query = _context.Invoices.Include(i => i.Client).Include(i => i.Items).AsQueryable();
            if (userId.HasValue)
            {
                query = query.Where(i => i.UserId == userId.Value);
            }
            var invoices = await query
                .Where(i => i.Client != null && i.Client.Name.ToLower() == clientName.ToLower())
                .ToListAsync();
            return invoices;
        }

        // PATCH: api/Invoices/bynumber/{invoiceNumber}
        [HttpPatch("bynumber/{invoiceNumber}")]
        public async Task<IActionResult> PatchInvoiceByNumber(string invoiceNumber, [FromBody] InvoicePatchDto patch, [FromQuery] int? userId)
        {
            var clean = (invoiceNumber ?? "").Trim().ToLower();
            var query = _context.Invoices.AsQueryable();
            if (userId.HasValue)
            {
                query = query.Where(i => i.UserId == userId.Value);
            }
            var invoice = await query.FirstOrDefaultAsync(i => i.InvoiceNumber.ToLower() == clean);
            if (invoice == null) return NotFound();

            if (patch.Status != null)
            {
                invoice.Status = patch.Status;
                if (!string.IsNullOrWhiteSpace(invoice.FullDataJson))
                {
                    try
                    {
                        var node = System.Text.Json.Nodes.JsonNode.Parse(invoice.FullDataJson);
                        if (node is System.Text.Json.Nodes.JsonObject obj)
                        {
                            obj["status"] = patch.Status;
                            invoice.FullDataJson = obj.ToJsonString();
                        }
                    }
                    catch { }
                }
            }

            await _context.SaveChangesAsync();
            return Ok(invoice);
        }

        // GET: api/Invoices
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Invoice>>> GetInvoices([FromQuery] int? userId)
        {
            var query = _context.Invoices.Include(i => i.Client).Include(i => i.Items).AsQueryable();
            if (userId.HasValue)
            {
                query = query.Where(i => i.UserId == userId.Value);
            }
            return await query.OrderByDescending(i => i.Id).ToListAsync();
        }

        // POST: api/Invoices
        [HttpPost]
        public async Task<ActionResult<Invoice>> PostInvoice(Invoice invoice)
        {
            var existing = await _context.Invoices
                .Include(i => i.Items)
                .FirstOrDefaultAsync(x => x.InvoiceNumber == invoice.InvoiceNumber && (invoice.UserId == null || x.UserId == invoice.UserId));

            if (existing != null)
            {
                existing.Date = invoice.Date;
                existing.DueDate = invoice.DueDate;
                existing.TotalAmount = invoice.TotalAmount;
                if (existing.Status == "Approved" && !invoice.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
                {
                    // preserve Approved status
                }
                else
                {
                    existing.Status = invoice.Status;
                }
                if (!string.IsNullOrEmpty(invoice.FullDataJson))
                {
                    existing.FullDataJson = invoice.FullDataJson;
                }

                // Update client association
                if (invoice.Client != null && !string.IsNullOrEmpty(invoice.Client.Name))
                {
                    var clientName = invoice.Client.Name.Trim();
                    var dbClient = await _context.Clients.FirstOrDefaultAsync(c => c.Name.ToLower() == clientName.ToLower());
                    if (dbClient != null)
                    {
                        existing.ClientId = dbClient.Id;
                    }
                    else if (!string.IsNullOrEmpty(clientName) && clientName != "Unknown Client")
                    {
                        var newClient = new Client { Name = clientName, UserId = invoice.UserId };
                        _context.Clients.Add(newClient);
                        await _context.SaveChangesAsync();
                        existing.ClientId = newClient.Id;
                    }
                }

                // Replace items if provided
                if (invoice.Items != null && invoice.Items.Count > 0)
                {
                    _context.InvoiceItems.RemoveRange(existing.Items);
                    foreach (var it in invoice.Items)
                    {
                        it.InvoiceId = existing.Id;
                        it.Id = 0; // ensure new ID is generated
                        _context.InvoiceItems.Add(it);
                    }
                }

                await _context.SaveChangesAsync();
                return Ok(existing);
            }

            // New invoice
            if (invoice.Client != null && !string.IsNullOrEmpty(invoice.Client.Name))
            {
                var clientName = invoice.Client.Name.Trim();
                var dbClient = await _context.Clients.FirstOrDefaultAsync(c => c.Name.ToLower() == clientName.ToLower());
                if (dbClient != null)
                {
                    invoice.ClientId = dbClient.Id;
                    invoice.Client = null;
                }
                else if (clientName != "Unknown Client")
                {
                    var newClient = new Client { Name = clientName, UserId = invoice.UserId };
                    _context.Clients.Add(newClient);
                    await _context.SaveChangesAsync();
                    invoice.ClientId = newClient.Id;
                    invoice.Client = null;
                }
            }

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetInvoice), new { id = invoice.Id.ToString() }, invoice);
        }

        // GET: api/Invoices/bynumber/INV-001
        [HttpGet("bynumber/{invoiceNumber}")]
        public async Task<ActionResult<Invoice>> GetInvoiceByNumber(string invoiceNumber)
        {
            var clean = (invoiceNumber ?? "").Trim().ToLower();
            var invoice = await _context.Invoices
                .Include(i => i.Client)
                .Include(i => i.Items)
                .FirstOrDefaultAsync(i => i.InvoiceNumber.ToLower() == clean);

            if (invoice == null) return NotFound();
            return invoice;
        }

        // GET: api/Invoices/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Invoice>> GetInvoice(string id)
        {
            var clean = (id ?? "").Trim().ToLower();
            Invoice? invoice = null;
            if (int.TryParse(id, out int intId))
            {
                invoice = await _context.Invoices
                    .Include(i => i.Client)
                    .Include(i => i.Items)
                    .FirstOrDefaultAsync(i => i.Id == intId || i.InvoiceNumber.ToLower() == clean);
            }
            else
            {
                invoice = await _context.Invoices
                    .Include(i => i.Client)
                    .Include(i => i.Items)
                    .FirstOrDefaultAsync(i => i.InvoiceNumber.ToLower() == clean);
            }

            if (invoice == null)
            {
                return NotFound();
            }

            return invoice;
        }

        // PATCH: api/Invoices/5
        [HttpPatch("{id:int}")]
        public async Task<IActionResult> PatchInvoice(int id, [FromBody] InvoicePatchDto patch)
        {
            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice == null) return NotFound();

            if (patch.Status != null)
            {
                invoice.Status = patch.Status;
                if (!string.IsNullOrWhiteSpace(invoice.FullDataJson))
                {
                    try
                    {
                        var node = System.Text.Json.Nodes.JsonNode.Parse(invoice.FullDataJson);
                        if (node is System.Text.Json.Nodes.JsonObject obj)
                        {
                            obj["status"] = patch.Status;
                            invoice.FullDataJson = obj.ToJsonString();
                        }
                    }
                    catch { }
                }
            }

            await _context.SaveChangesAsync();
            return Ok(invoice);
        }


        // DELETE: api/Invoices/5 or api/Invoices/INV-001
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteInvoice(string id)
        {
            var clean = (id ?? "").Trim().ToLower();
            Invoice? invoice = null;
            if (int.TryParse(id, out int intId))
            {
                invoice = await _context.Invoices.Include(i => i.Items).FirstOrDefaultAsync(i => i.Id == intId);
            }
            if (invoice == null)
            {
                invoice = await _context.Invoices.Include(i => i.Items).FirstOrDefaultAsync(i => i.InvoiceNumber.ToLower() == clean);
            }
            if (invoice == null) return NotFound();

            if (invoice.Status.Equals("Pending Approval", StringComparison.OrdinalIgnoreCase) || invoice.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "Cannot delete a document that is Pending Approval or Approved." });
            }

            if (invoice.Items != null && invoice.Items.Count > 0)
            {
                _context.InvoiceItems.RemoveRange(invoice.Items);
            }

            _context.Invoices.Remove(invoice);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }

    public class InvoicePatchDto
    {
        public string? Status { get; set; }
    }
}
