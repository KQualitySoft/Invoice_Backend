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
    public class DeliveryChallansController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DeliveryChallansController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<DeliveryChallan>>> GetDeliveryChallans([FromQuery] int? userId)
        {
            var query = _context.DeliveryChallans.Include(d => d.Client).Include(d => d.Items).AsQueryable();
            if (userId.HasValue)
            {
                query = query.Where(d => d.UserId == userId.Value);
            }
            return await query.OrderByDescending(d => d.Id).ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<DeliveryChallan>> PostDeliveryChallan(DeliveryChallan deliveryChallan)
        {
            var existing = await _context.DeliveryChallans
                .Include(d => d.Items)
                .FirstOrDefaultAsync(d => d.ChallanNumber == deliveryChallan.ChallanNumber && (deliveryChallan.UserId == null || d.UserId == deliveryChallan.UserId));

            if (existing != null)
            {
                existing.Date = deliveryChallan.Date;
                existing.DueDate = deliveryChallan.DueDate;
                if (existing.Status == "Approved" && !deliveryChallan.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
                {
                    // preserve Approved
                }
                else
                {
                    existing.Status = deliveryChallan.Status;
                }
                existing.TotalAmount = deliveryChallan.TotalAmount;
                if (!string.IsNullOrEmpty(deliveryChallan.FullDataJson))
                {
                    existing.FullDataJson = deliveryChallan.FullDataJson;
                }

                if (deliveryChallan.Client != null && !string.IsNullOrEmpty(deliveryChallan.Client.Name))
                {
                    var clientName = deliveryChallan.Client.Name.Trim();
                    var dbClient = await _context.Clients.FirstOrDefaultAsync(c => c.Name.ToLower() == clientName.ToLower());
                    if (dbClient != null)
                    {
                        existing.ClientId = dbClient.Id;
                    }
                    else if (!string.IsNullOrEmpty(clientName) && clientName != "Unknown Client")
                    {
                        var newClient = new Client { Name = clientName, UserId = deliveryChallan.UserId };
                        _context.Clients.Add(newClient);
                        await _context.SaveChangesAsync();
                        existing.ClientId = newClient.Id;
                    }
                }

                if (deliveryChallan.Items != null && deliveryChallan.Items.Count > 0)
                {
                    _context.DeliveryChallanItems.RemoveRange(existing.Items);
                    foreach (var it in deliveryChallan.Items)
                    {
                        it.DeliveryChallanId = existing.Id;
                        it.Id = 0;
                        _context.DeliveryChallanItems.Add(it);
                    }
                }
                
                await _context.SaveChangesAsync();
                return Ok(existing);
            }

            if (deliveryChallan.Client != null && !string.IsNullOrEmpty(deliveryChallan.Client.Name))
            {
                var clientName = deliveryChallan.Client.Name.Trim();
                var dbClient = await _context.Clients.FirstOrDefaultAsync(c => c.Name.ToLower() == clientName.ToLower());
                if (dbClient != null)
                {
                    deliveryChallan.ClientId = dbClient.Id;
                    deliveryChallan.Client = null;
                }
                else if (clientName != "Unknown Client")
                {
                    var newClient = new Client { Name = clientName, UserId = deliveryChallan.UserId };
                    _context.Clients.Add(newClient);
                    await _context.SaveChangesAsync();
                    deliveryChallan.ClientId = newClient.Id;
                    deliveryChallan.Client = null;
                }
            }
            
            _context.DeliveryChallans.Add(deliveryChallan);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetDeliveryChallan), new { id = deliveryChallan.Id }, deliveryChallan);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<DeliveryChallan>> GetDeliveryChallan(string id)
        {
            var clean = (id ?? "").Trim().ToLower();
            DeliveryChallan? deliveryChallan = null;
            if (int.TryParse(id, out int intId))
            {
                deliveryChallan = await _context.DeliveryChallans
                    .Include(d => d.Client)
                    .Include(d => d.Items)
                    .FirstOrDefaultAsync(d => d.Id == intId || d.ChallanNumber.ToLower() == clean);
            }
            else
            {
                deliveryChallan = await _context.DeliveryChallans
                    .Include(d => d.Client)
                    .Include(d => d.Items)
                    .FirstOrDefaultAsync(d => d.ChallanNumber.ToLower() == clean);
            }

            if (deliveryChallan == null) return NotFound();
            return deliveryChallan;
        }

        [HttpGet("bynumber/{challanNumber}")]
        public async Task<ActionResult<DeliveryChallan>> GetDeliveryChallanByNumber(string challanNumber)
        {
            var clean = (challanNumber ?? "").Trim().ToLower();
            var deliveryChallan = await _context.DeliveryChallans
                .Include(d => d.Client)
                .Include(d => d.Items)
                .FirstOrDefaultAsync(d => d.ChallanNumber.ToLower() == clean);

            if (deliveryChallan == null) return NotFound();
            return deliveryChallan;
        }

        // PATCH: api/DeliveryChallans/5 or api/DeliveryChallans/DC-001
        [HttpPatch("{id}")]
        public async Task<IActionResult> PatchDeliveryChallan(string id, [FromBody] DocumentPatchDto patch)
        {
            var clean = (id ?? "").Trim().ToLower();
            DeliveryChallan? deliveryChallan = null;
            if (int.TryParse(id, out int intId))
            {
                deliveryChallan = await _context.DeliveryChallans.FindAsync(intId);
            }
            if (deliveryChallan == null)
            {
                deliveryChallan = await _context.DeliveryChallans.FirstOrDefaultAsync(d => d.ChallanNumber.ToLower() == clean);
            }
            if (deliveryChallan == null) return NotFound();

            if (patch.Status != null)
            {
                deliveryChallan.Status = patch.Status;
                if (!string.IsNullOrWhiteSpace(deliveryChallan.FullDataJson))
                {
                    try
                    {
                        var node = System.Text.Json.Nodes.JsonNode.Parse(deliveryChallan.FullDataJson);
                        if (node is System.Text.Json.Nodes.JsonObject obj)
                        {
                            obj["status"] = patch.Status;
                            deliveryChallan.FullDataJson = obj.ToJsonString();
                        }
                    }
                    catch { }
                }
            }

            await _context.SaveChangesAsync();
            return Ok(deliveryChallan);
        }

        // PATCH: api/DeliveryChallans/bynumber/{challanNumber}
        [HttpPatch("bynumber/{challanNumber}")]
        public async Task<IActionResult> PatchDeliveryChallanByNumber(string challanNumber, [FromBody] DocumentPatchDto patch)
        {
            return await PatchDeliveryChallan(challanNumber, patch);
        }

        // DELETE: api/DeliveryChallans/5 or api/DeliveryChallans/DC-001
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDeliveryChallan(string id)
        {
            var clean = (id ?? "").Trim().ToLower();
            DeliveryChallan? deliveryChallan = null;
            if (int.TryParse(id, out int intId))
            {
                deliveryChallan = await _context.DeliveryChallans.Include(d => d.Items).FirstOrDefaultAsync(d => d.Id == intId);
            }
            if (deliveryChallan == null)
            {
                deliveryChallan = await _context.DeliveryChallans.Include(d => d.Items).FirstOrDefaultAsync(d => d.ChallanNumber.ToLower() == clean);
            }
            if (deliveryChallan == null) return NotFound();

            if (deliveryChallan.Status != null && (deliveryChallan.Status.Equals("Pending Approval", StringComparison.OrdinalIgnoreCase) || deliveryChallan.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest(new { error = "Cannot delete a document that is Pending Approval or Approved." });
            }

            if (deliveryChallan.Items != null && deliveryChallan.Items.Count > 0)
            {
                _context.DeliveryChallanItems.RemoveRange(deliveryChallan.Items);
            }

            _context.DeliveryChallans.Remove(deliveryChallan);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
