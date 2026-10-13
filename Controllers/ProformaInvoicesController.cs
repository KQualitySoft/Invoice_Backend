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
    public class ProformaInvoicesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProformaInvoicesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProformaInvoice>>> GetProformaInvoices([FromQuery] int? userId)
        {
            var query = _context.ProformaInvoices.Include(p => p.Client).Include(p => p.Items).AsQueryable();
            if (userId.HasValue)
            {
                query = query.Where(p => p.UserId == userId.Value);
            }
            var list = await query.OrderByDescending(p => p.Id).ToListAsync();
            bool needSave = false;
            foreach (var p in list)
            {
                if (string.IsNullOrEmpty(p.ProformaNumber) && !string.IsNullOrEmpty(p.FullDataJson))
                {
                    try
                    {
                        var node = System.Text.Json.Nodes.JsonNode.Parse(p.FullDataJson);
                        var num = node?["proformaNumber"]?.ToString() ?? node?["proformaNo"]?.ToString() ?? node?["invoiceNo"]?.ToString() ?? node?["number"]?.ToString();
                        if (!string.IsNullOrEmpty(num))
                        {
                            p.ProformaNumber = num;
                            needSave = true;
                        }
                    }
                    catch { }
                }
            }
            if (needSave)
            {
                try { await _context.SaveChangesAsync(); } catch { }
            }
            return list;
        }

        [HttpPost]
        public async Task<ActionResult<ProformaInvoice>> PostProformaInvoice(ProformaInvoice proformaInvoice)
        {
            if (string.IsNullOrEmpty(proformaInvoice.ProformaNumber) && !string.IsNullOrEmpty(proformaInvoice.FullDataJson))
            {
                try
                {
                    var node = System.Text.Json.Nodes.JsonNode.Parse(proformaInvoice.FullDataJson);
                    var num = node?["proformaNumber"]?.ToString() ?? node?["proformaNo"]?.ToString() ?? node?["invoiceNo"]?.ToString() ?? node?["number"]?.ToString();
                    if (!string.IsNullOrEmpty(num)) proformaInvoice.ProformaNumber = num;
                }
                catch { }
            }

            var existing = await _context.ProformaInvoices
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.ProformaNumber == proformaInvoice.ProformaNumber && (proformaInvoice.UserId == null || p.UserId == proformaInvoice.UserId));

            if (existing != null)
            {
                existing.Date = proformaInvoice.Date;
                existing.DueDate = proformaInvoice.DueDate;
                existing.TotalAmount = proformaInvoice.TotalAmount;
                if (existing.Status == "Approved" && !proformaInvoice.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
                {
                    // preserve Approved
                }
                else
                {
                    existing.Status = proformaInvoice.Status;
                }
                if (!string.IsNullOrEmpty(proformaInvoice.FullDataJson))
                {
                    existing.FullDataJson = proformaInvoice.FullDataJson;
                }

                if (proformaInvoice.Client != null && !string.IsNullOrEmpty(proformaInvoice.Client.Name))
                {
                    var clientName = proformaInvoice.Client.Name.Trim();
                    var dbClient = await _context.Clients.FirstOrDefaultAsync(c => c.Name.ToLower() == clientName.ToLower());
                    if (dbClient != null)
                    {
                        existing.ClientId = dbClient.Id;
                    }
                    else if (!string.IsNullOrEmpty(clientName) && clientName != "Unknown Client")
                    {
                        var newClient = new Client { Name = clientName, UserId = proformaInvoice.UserId };
                        _context.Clients.Add(newClient);
                        await _context.SaveChangesAsync();
                        existing.ClientId = newClient.Id;
                    }
                }

                if (proformaInvoice.Items != null && proformaInvoice.Items.Count > 0)
                {
                    _context.ProformaInvoiceItems.RemoveRange(existing.Items);
                    foreach (var it in proformaInvoice.Items)
                    {
                        it.ProformaInvoiceId = existing.Id;
                        it.Id = 0;
                        _context.ProformaInvoiceItems.Add(it);
                    }
                }
                
                await _context.SaveChangesAsync();
                return Ok(existing);
            }

            if (proformaInvoice.Client != null && !string.IsNullOrEmpty(proformaInvoice.Client.Name))
            {
                var clientName = proformaInvoice.Client.Name.Trim();
                var dbClient = await _context.Clients.FirstOrDefaultAsync(c => c.Name.ToLower() == clientName.ToLower());
                if (dbClient != null)
                {
                    proformaInvoice.ClientId = dbClient.Id;
                    proformaInvoice.Client = null;
                }
                else if (clientName != "Unknown Client")
                {
                    var newClient = new Client { Name = clientName, UserId = proformaInvoice.UserId };
                    _context.Clients.Add(newClient);
                    await _context.SaveChangesAsync();
                    proformaInvoice.ClientId = newClient.Id;
                    proformaInvoice.Client = null;
                }
            }
            
            _context.ProformaInvoices.Add(proformaInvoice);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetProformaInvoice), new { id = proformaInvoice.Id }, proformaInvoice);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ProformaInvoice>> GetProformaInvoice(string id)
        {
            var clean = (id ?? "").Trim().ToLower();
            ProformaInvoice? proformaInvoice = null;
            if (int.TryParse(id, out int intId))
            {
                proformaInvoice = await _context.ProformaInvoices
                    .Include(p => p.Client)
                    .Include(p => p.Items)
                    .FirstOrDefaultAsync(p => p.Id == intId || p.ProformaNumber.ToLower() == clean);
            }
            else
            {
                proformaInvoice = await _context.ProformaInvoices
                    .Include(p => p.Client)
                    .Include(p => p.Items)
                    .FirstOrDefaultAsync(p => p.ProformaNumber.ToLower() == clean);
            }

            if (proformaInvoice == null) return NotFound();
            return proformaInvoice;
        }

        [HttpGet("bynumber/{proformaNumber}")]
        public async Task<ActionResult<ProformaInvoice>> GetProformaInvoiceByNumber(string proformaNumber)
        {
            var clean = (proformaNumber ?? "").Trim().ToLower();
            ProformaInvoice? proformaInvoice = null;

            if (int.TryParse(clean, out int intId))
            {
                proformaInvoice = await _context.ProformaInvoices
                    .Include(p => p.Client)
                    .Include(p => p.Items)
                    .FirstOrDefaultAsync(p => p.Id == intId);
            }

            if (proformaInvoice == null && clean != "—")
            {
                proformaInvoice = await _context.ProformaInvoices
                    .Include(p => p.Client)
                    .Include(p => p.Items)
                    .FirstOrDefaultAsync(p => p.ProformaNumber.ToLower() == clean);
            }

            if (proformaInvoice == null)
            {
                var all = await _context.ProformaInvoices
                    .Include(p => p.Client)
                    .Include(p => p.Items)
                    .ToListAsync();
                proformaInvoice = all.FirstOrDefault(p =>
                {
                    if (string.IsNullOrEmpty(p.FullDataJson)) return false;
                    return clean != "—" && p.FullDataJson.IndexOf($"\"{clean}\"", StringComparison.OrdinalIgnoreCase) >= 0;
                });
            }

            if (proformaInvoice == null) return NotFound();
            return proformaInvoice;
        }

        // PATCH: api/ProformaInvoices/5 or api/ProformaInvoices/PI-001
        [HttpPatch("{id}")]
        public async Task<IActionResult> PatchProformaInvoice(string id, [FromBody] DocumentPatchDto patch)
        {
            var clean = (id ?? "").Trim().ToLower();
            ProformaInvoice? proformaInvoice = null;
            if (int.TryParse(id, out int intId))
            {
                proformaInvoice = await _context.ProformaInvoices.FindAsync(intId);
            }
            if (proformaInvoice == null)
            {
                proformaInvoice = await _context.ProformaInvoices.FirstOrDefaultAsync(p => p.ProformaNumber.ToLower() == clean);
            }
            if (proformaInvoice == null) return NotFound();

            if (patch.Status != null)
            {
                proformaInvoice.Status = patch.Status;
                if (!string.IsNullOrWhiteSpace(proformaInvoice.FullDataJson))
                {
                    try
                    {
                        var node = System.Text.Json.Nodes.JsonNode.Parse(proformaInvoice.FullDataJson);
                        if (node is System.Text.Json.Nodes.JsonObject obj)
                        {
                            obj["status"] = patch.Status;
                            proformaInvoice.FullDataJson = obj.ToJsonString();
                        }
                    }
                    catch { }
                }
            }

            await _context.SaveChangesAsync();
            return Ok(proformaInvoice);
        }

        // PATCH: api/ProformaInvoices/bynumber/{proformaNumber}
        [HttpPatch("bynumber/{proformaNumber}")]
        public async Task<IActionResult> PatchProformaInvoiceByNumber(string proformaNumber, [FromBody] DocumentPatchDto patch)
        {
            return await PatchProformaInvoice(proformaNumber, patch);
        }

        // DELETE: api/ProformaInvoices/5 or api/ProformaInvoices/PI-001
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProformaInvoice(string id)
        {
            var clean = (id ?? "").Trim().ToLower();
            ProformaInvoice? proformaInvoice = null;
            if (int.TryParse(id, out int intId))
            {
                proformaInvoice = await _context.ProformaInvoices.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == intId);
            }
            if (proformaInvoice == null)
            {
                proformaInvoice = await _context.ProformaInvoices.Include(p => p.Items).FirstOrDefaultAsync(p => p.ProformaNumber.ToLower() == clean);
            }
            if (proformaInvoice == null) return NotFound();

            if (proformaInvoice.Status != null && (proformaInvoice.Status.Equals("Pending Approval", StringComparison.OrdinalIgnoreCase) || proformaInvoice.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest(new { error = "Cannot delete a document that is Pending Approval or Approved." });
            }

            if (proformaInvoice.Items != null && proformaInvoice.Items.Count > 0)
            {
                _context.ProformaInvoiceItems.RemoveRange(proformaInvoice.Items);
            }

            _context.ProformaInvoices.Remove(proformaInvoice);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
