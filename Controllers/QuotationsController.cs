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
    public class QuotationsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public QuotationsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Quotation>>> GetQuotations([FromQuery] int? userId)
        {
            var query = _context.Quotations.Include(q => q.Client).Include(q => q.Items).AsQueryable();
            if (userId.HasValue)
            {
                query = query.Where(q => q.UserId == userId.Value);
            }
            return await query.OrderByDescending(q => q.Id).ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<Quotation>> PostQuotation(Quotation quotation)
        {
            var existing = await _context.Quotations
                .Include(q => q.Items)
                .FirstOrDefaultAsync(q => q.QuotationNumber == quotation.QuotationNumber && (quotation.UserId == null || q.UserId == quotation.UserId));

            if (existing != null)
            {
                existing.Date = quotation.Date;
                existing.DueDate = quotation.DueDate;
                existing.TotalAmount = quotation.TotalAmount;
                if (existing.Status == "Approved" && !quotation.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
                {
                    // preserve Approved
                }
                else
                {
                    existing.Status = quotation.Status;
                }
                if (!string.IsNullOrEmpty(quotation.FullDataJson))
                {
                    existing.FullDataJson = quotation.FullDataJson;
                }

                if (quotation.Client != null && !string.IsNullOrEmpty(quotation.Client.Name))
                {
                    var clientName = quotation.Client.Name.Trim();
                    var dbClient = await _context.Clients.FirstOrDefaultAsync(c => c.Name.ToLower() == clientName.ToLower());
                    if (dbClient != null)
                    {
                        existing.ClientId = dbClient.Id;
                    }
                    else if (!string.IsNullOrEmpty(clientName) && clientName != "Unknown Client")
                    {
                        var newClient = new Client { Name = clientName, UserId = quotation.UserId };
                        _context.Clients.Add(newClient);
                        await _context.SaveChangesAsync();
                        existing.ClientId = newClient.Id;
                    }
                }

                if (quotation.Items != null && quotation.Items.Count > 0)
                {
                    _context.QuotationItems.RemoveRange(existing.Items);
                    foreach (var it in quotation.Items)
                    {
                        it.QuotationId = existing.Id;
                        it.Id = 0;
                        _context.QuotationItems.Add(it);
                    }
                }
                
                await _context.SaveChangesAsync();
                return Ok(existing);
            }

            if (quotation.Client != null && !string.IsNullOrEmpty(quotation.Client.Name))
            {
                var clientName = quotation.Client.Name.Trim();
                var dbClient = await _context.Clients.FirstOrDefaultAsync(c => c.Name.ToLower() == clientName.ToLower());
                if (dbClient != null)
                {
                    quotation.ClientId = dbClient.Id;
                    quotation.Client = null;
                }
                else if (clientName != "Unknown Client")
                {
                    var newClient = new Client { Name = clientName, UserId = quotation.UserId };
                    _context.Clients.Add(newClient);
                    await _context.SaveChangesAsync();
                    quotation.ClientId = newClient.Id;
                    quotation.Client = null;
                }
            }
            
            _context.Quotations.Add(quotation);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetQuotation), new { id = quotation.Id }, quotation);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Quotation>> GetQuotation(string id)
        {
            var clean = (id ?? "").Trim().ToLower();
            Quotation? quotation = null;
            if (int.TryParse(id, out int intId))
            {
                quotation = await _context.Quotations
                    .Include(q => q.Client)
                    .Include(q => q.Items)
                    .FirstOrDefaultAsync(q => q.Id == intId || q.QuotationNumber.ToLower() == clean);
            }
            else
            {
                quotation = await _context.Quotations
                    .Include(q => q.Client)
                    .Include(q => q.Items)
                    .FirstOrDefaultAsync(q => q.QuotationNumber.ToLower() == clean);
            }

            if (quotation == null) return NotFound();
            return quotation;
        }

        [HttpGet("bynumber/{quotationNumber}")]
        public async Task<ActionResult<Quotation>> GetQuotationByNumber(string quotationNumber)
        {
            var clean = (quotationNumber ?? "").Trim().ToLower();
            var quotation = await _context.Quotations
                .Include(q => q.Client)
                .Include(q => q.Items)
                .FirstOrDefaultAsync(q => q.QuotationNumber.ToLower() == clean);

            if (quotation == null) return NotFound();
            return quotation;
        }

        // PATCH: api/Quotations/5 or api/Quotations/QUOT-001
        [HttpPatch("{id}")]
        public async Task<IActionResult> PatchQuotation(string id, [FromBody] DocumentPatchDto patch)
        {
            var clean = (id ?? "").Trim().ToLower();
            Quotation? quotation = null;
            if (int.TryParse(id, out int intId))
            {
                quotation = await _context.Quotations.FindAsync(intId);
            }
            if (quotation == null)
            {
                quotation = await _context.Quotations.FirstOrDefaultAsync(q => q.QuotationNumber.ToLower() == clean);
            }
            if (quotation == null) return NotFound();

            if (patch.Status != null)
            {
                quotation.Status = patch.Status;
                if (!string.IsNullOrWhiteSpace(quotation.FullDataJson))
                {
                    try
                    {
                        var node = System.Text.Json.Nodes.JsonNode.Parse(quotation.FullDataJson);
                        if (node is System.Text.Json.Nodes.JsonObject obj)
                        {
                            obj["status"] = patch.Status;
                            quotation.FullDataJson = obj.ToJsonString();
                        }
                    }
                    catch { }
                }
            }

            await _context.SaveChangesAsync();
            return Ok(quotation);
        }

        // PATCH: api/Quotations/bynumber/{quotationNumber}
        [HttpPatch("bynumber/{quotationNumber}")]
        public async Task<IActionResult> PatchQuotationByNumber(string quotationNumber, [FromBody] DocumentPatchDto patch)
        {
            return await PatchQuotation(quotationNumber, patch);
        }

        // DELETE: api/Quotations/5 or api/Quotations/QUOT-001
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteQuotation(string id)
        {
            var clean = (id ?? "").Trim().ToLower();
            Quotation? quotation = null;
            if (int.TryParse(id, out int intId))
            {
                quotation = await _context.Quotations.Include(q => q.Items).FirstOrDefaultAsync(q => q.Id == intId);
            }
            if (quotation == null)
            {
                quotation = await _context.Quotations.Include(q => q.Items).FirstOrDefaultAsync(q => q.QuotationNumber.ToLower() == clean);
            }
            if (quotation == null) return NotFound();

            if (quotation.Status != null && (quotation.Status.Equals("Pending Approval", StringComparison.OrdinalIgnoreCase) || quotation.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest(new { error = "Cannot delete a document that is Pending Approval or Approved." });
            }

            if (quotation.Items != null && quotation.Items.Count > 0)
            {
                _context.QuotationItems.RemoveRange(quotation.Items);
            }

            _context.Quotations.Remove(quotation);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
