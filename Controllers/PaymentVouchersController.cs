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
    public class PaymentVouchersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PaymentVouchersController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PaymentVoucher>>> GetPaymentVouchers([FromQuery] int? userId)
        {
            var query = _context.PaymentVouchers.Include(p => p.Client).AsQueryable();
            if (userId.HasValue)
            {
                query = query.Where(p => p.UserId == userId.Value);
            }
            return await query.OrderByDescending(p => p.Id).ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<PaymentVoucher>> PostPaymentVoucher(PaymentVoucher paymentVoucher)
        {
            var cleanVoucherNum = (paymentVoucher.VoucherNumber ?? "").Trim().ToLower();
            var existing = await _context.PaymentVouchers
                .FirstOrDefaultAsync(p => (paymentVoucher.Id > 0 && p.Id == paymentVoucher.Id) ||
                                          (!string.IsNullOrEmpty(cleanVoucherNum) && p.VoucherNumber.ToLower() == cleanVoucherNum));
            if (existing != null)
            {
                existing.Date = paymentVoucher.Date;
                existing.Amount = paymentVoucher.Amount;
                existing.PaymentMethod = paymentVoucher.PaymentMethod;

                // Protect Approved status from accidental downgrade to Draft
                if (existing.Status == "Approved" && !paymentVoucher.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
                {
                    // keep Approved status
                }
                else
                {
                    existing.Status = paymentVoucher.Status;
                }

                if (!string.IsNullOrEmpty(paymentVoucher.FullDataJson))
                {
                    if (!string.IsNullOrEmpty(existing.FullDataJson))
                    {
                        try
                        {
                            var existingNode = System.Text.Json.Nodes.JsonNode.Parse(existing.FullDataJson) as System.Text.Json.Nodes.JsonObject;
                            var incomingNode = System.Text.Json.Nodes.JsonNode.Parse(paymentVoucher.FullDataJson) as System.Text.Json.Nodes.JsonObject;
                            if (existingNode != null && incomingNode != null)
                            {
                                if (string.IsNullOrWhiteSpace(incomingNode["receivedSign"]?.ToString()) &&
                                    !string.IsNullOrWhiteSpace(existingNode["receivedSign"]?.ToString()))
                                {
                                    incomingNode["receivedSign"] = existingNode["receivedSign"]?.ToString();
                                }
                                if (string.IsNullOrWhiteSpace(incomingNode["receivedDate"]?.ToString()) &&
                                    !string.IsNullOrWhiteSpace(existingNode["receivedDate"]?.ToString()))
                                {
                                    incomingNode["receivedDate"] = existingNode["receivedDate"]?.ToString();
                                }
                                if (string.IsNullOrWhiteSpace(incomingNode["approvedSign"]?.ToString()))
                                {
                                    if (!string.IsNullOrWhiteSpace(existingNode["approvedSign"]?.ToString()))
                                    {
                                        incomingNode["approvedSign"] = existingNode["approvedSign"]?.ToString();
                                    }
                                    else if (existing.Status == "Approved" || paymentVoucher.Status == "Approved")
                                    {
                                        var appBy = incomingNode["approvedBy"]?.ToString() ?? existingNode["approvedBy"]?.ToString() ?? "";
                                        if (!string.IsNullOrWhiteSpace(appBy))
                                        {
                                            incomingNode["approvedSign"] = appBy.Contains("@") ? appBy.Split('@')[0] : appBy;
                                        }
                                    }
                                }
                                if (string.IsNullOrWhiteSpace(incomingNode["approvedDate"]?.ToString()))
                                {
                                    if (!string.IsNullOrWhiteSpace(existingNode["approvedDate"]?.ToString()))
                                    {
                                        incomingNode["approvedDate"] = existingNode["approvedDate"]?.ToString();
                                    }
                                    else if (existing.Status == "Approved" || paymentVoucher.Status == "Approved")
                                    {
                                        var appAt = incomingNode["approvedAt"]?.ToString() ?? existingNode["approvedAt"]?.ToString() ?? "";
                                        incomingNode["approvedDate"] = !string.IsNullOrWhiteSpace(appAt)
                                            ? (appAt.Contains("T") ? appAt.Split('T')[0] : appAt)
                                            : DateTime.UtcNow.ToString("yyyy-MM-dd");
                                    }
                                }
                                existing.FullDataJson = incomingNode.ToJsonString();
                            }
                            else
                            {
                                existing.FullDataJson = paymentVoucher.FullDataJson;
                            }
                        }
                        catch
                        {
                            existing.FullDataJson = paymentVoucher.FullDataJson;
                        }
                    }
                    else
                    {
                        existing.FullDataJson = paymentVoucher.FullDataJson;
                    }
                }
                if (paymentVoucher.UserId.HasValue)
                {
                    existing.UserId = paymentVoucher.UserId;
                }

                if (paymentVoucher.Client != null && !string.IsNullOrEmpty(paymentVoucher.Client.Name))
                {
                    var clientName = paymentVoucher.Client.Name.Trim();
                    var dbClient = await _context.Clients.FirstOrDefaultAsync(c => c.Name.ToLower() == clientName.ToLower());
                    if (dbClient != null)
                    {
                        existing.ClientId = dbClient.Id;
                    }
                    else if (!string.IsNullOrEmpty(clientName) && clientName != "Unknown Client")
                    {
                        var newClient = new Client { Name = clientName, UserId = paymentVoucher.UserId };
                        _context.Clients.Add(newClient);
                        await _context.SaveChangesAsync();
                        existing.ClientId = newClient.Id;
                    }
                }
                
                await _context.SaveChangesAsync();
                return Ok(existing);
            }
            
            if (paymentVoucher.Client != null && !string.IsNullOrEmpty(paymentVoucher.Client.Name))
            {
                var clientName = paymentVoucher.Client.Name.Trim();
                var dbClient = await _context.Clients.FirstOrDefaultAsync(c => c.Name.ToLower() == clientName.ToLower());
                if (dbClient != null)
                {
                    paymentVoucher.ClientId = dbClient.Id;
                    paymentVoucher.Client = null;
                }
                else if (clientName != "Unknown Client")
                {
                    var newClient = new Client { Name = clientName, UserId = paymentVoucher.UserId };
                    _context.Clients.Add(newClient);
                    await _context.SaveChangesAsync();
                    paymentVoucher.ClientId = newClient.Id;
                    paymentVoucher.Client = null;
                }
            }
            
            _context.PaymentVouchers.Add(paymentVoucher);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetPaymentVoucher), new { id = paymentVoucher.Id }, paymentVoucher);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PaymentVoucher>> GetPaymentVoucher(string id)
        {
            var clean = (id ?? "").Trim().ToLower();
            PaymentVoucher? paymentVoucher = null;
            if (int.TryParse(id, out int intId))
            {
                paymentVoucher = await _context.PaymentVouchers.Include(p => p.Client)
                    .FirstOrDefaultAsync(p => p.Id == intId || p.VoucherNumber.ToLower() == clean);
            }
            else
            {
                paymentVoucher = await _context.PaymentVouchers.Include(p => p.Client)
                    .FirstOrDefaultAsync(p => p.VoucherNumber.ToLower() == clean);
            }

            if (paymentVoucher == null) return NotFound();
            return paymentVoucher;
        }

        [HttpGet("bynumber/{voucherNumber}")]
        public async Task<ActionResult<PaymentVoucher>> GetPaymentVoucherByNumber(string voucherNumber)
        {
            var clean = (voucherNumber ?? "").Trim().ToLower();
            var paymentVoucher = await _context.PaymentVouchers.Include(p => p.Client)
                .FirstOrDefaultAsync(p => p.VoucherNumber.ToLower() == clean);
            if (paymentVoucher == null) return NotFound();
            return paymentVoucher;
        }

        // PATCH: api/PaymentVouchers/5 or api/PaymentVouchers/PV-001
        [HttpPatch("{id}")]
        public async Task<IActionResult> PatchPaymentVoucher(string id, [FromBody] DocumentPatchDto patch)
        {
            var clean = (id ?? "").Trim().ToLower();
            PaymentVoucher? paymentVoucher = null;
            if (int.TryParse(id, out int intId))
            {
                paymentVoucher = await _context.PaymentVouchers.FindAsync(intId);
            }
            if (paymentVoucher == null)
            {
                paymentVoucher = await _context.PaymentVouchers.FirstOrDefaultAsync(p => p.VoucherNumber.ToLower() == clean);
            }
            if (paymentVoucher == null) return NotFound();

            if (patch.Status != null)
            {
                paymentVoucher.Status = patch.Status;
                if (!string.IsNullOrWhiteSpace(paymentVoucher.FullDataJson))
                {
                    try
                    {
                        var node = System.Text.Json.Nodes.JsonNode.Parse(paymentVoucher.FullDataJson);
                        if (node is System.Text.Json.Nodes.JsonObject obj)
                        {
                            obj["status"] = patch.Status;
                            paymentVoucher.FullDataJson = obj.ToJsonString();
                        }
                    }
                    catch { }
                }
            }

            await _context.SaveChangesAsync();
            return Ok(paymentVoucher);
        }

        // PATCH: api/PaymentVouchers/bynumber/{voucherNumber}
        [HttpPatch("bynumber/{voucherNumber}")]
        public async Task<IActionResult> PatchPaymentVoucherByNumber(string voucherNumber, [FromBody] DocumentPatchDto patch)
        {
            return await PatchPaymentVoucher(voucherNumber, patch);
        }

        // DELETE: api/PaymentVouchers/5 or api/PaymentVouchers/PV-001
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePaymentVoucher(string id)
        {
            var clean = (id ?? "").Trim().ToLower();
            PaymentVoucher? paymentVoucher = null;
            if (int.TryParse(id, out int intId))
            {
                paymentVoucher = await _context.PaymentVouchers.FindAsync(intId);
            }
            if (paymentVoucher == null)
            {
                paymentVoucher = await _context.PaymentVouchers.FirstOrDefaultAsync(p => p.VoucherNumber.ToLower() == clean);
            }
            if (paymentVoucher == null) return NotFound();

            if (paymentVoucher.Status != null && (paymentVoucher.Status.Equals("Pending Approval", StringComparison.OrdinalIgnoreCase) || paymentVoucher.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest(new { error = "Cannot delete a document that is Pending Approval or Approved." });
            }

            _context.PaymentVouchers.Remove(paymentVoucher);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
