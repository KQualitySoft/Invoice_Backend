using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoiceGen.Data;
using InvoiceGen.Models;

namespace InvoiceGen.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ApprovalsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ApprovalsController(AppDbContext context)
        {
            _context = context;
        }

        public class ApprovalActionDto
        {
            private string _docType = string.Empty;
            public string DocumentType 
            { 
                get => !string.IsNullOrEmpty(_docType) ? _docType : DocType ?? string.Empty; 
                set => _docType = value; 
            }
            public string? DocType { get; set; }

            private string _docNum = string.Empty;
            public string DocumentNumber 
            { 
                get => !string.IsNullOrEmpty(_docNum) ? _docNum : DocNumber ?? string.Empty; 
                set => _docNum = value; 
            }
            public string? DocNumber { get; set; }

            public int? DocumentId { get; set; }
            public int? DocId { get; set; }
            public string UserEmail { get; set; } = string.Empty;
            public int? UserId { get; set; }
            public string? UserRole { get; set; }
            public string? Comments { get; set; }
            public string? Reason { get; set; }
            public string? ApprovedSign { get; set; }
            public string? ApprovedDate { get; set; }
        }

        private string GetCallerRole(ApprovalActionDto dto)
        {
            if (Request.Headers.TryGetValue("X-User-Role", out var headerVal) && !string.IsNullOrEmpty(headerVal))
            {
                return headerVal.ToString();
            }
            return dto.UserRole ?? "User";
        }

        private void UpdateFullDataJsonStatus(ref string? fullDataJson, string newStatus, string? action = null, string? userEmail = null, string? comments = null, string? approvedSign = null, string? approvedDate = null)
        {
            if (string.IsNullOrWhiteSpace(fullDataJson)) return;
            try
            {
                var node = JsonNode.Parse(fullDataJson);
                if (node is JsonObject obj)
                {
                    obj["status"] = newStatus;
                    if (newStatus.Equals("Approved", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(userEmail)) obj["approvedBy"] = userEmail;
                        obj["approvedAt"] = DateTime.UtcNow.ToString("o");
                        if (!string.IsNullOrEmpty(comments)) obj["approvalComments"] = comments;
                        if (!string.IsNullOrEmpty(approvedSign)) obj["approvedSign"] = approvedSign;
                        else if (!string.IsNullOrEmpty(userEmail)) obj["approvedSign"] = userEmail.Contains("@") ? userEmail.Split('@')[0] : userEmail;
                        if (!string.IsNullOrEmpty(approvedDate)) obj["approvedDate"] = approvedDate;
                        else obj["approvedDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd");
                    }
                    else if (newStatus.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(userEmail)) obj["rejectedBy"] = userEmail;
                        obj["rejectedAt"] = DateTime.UtcNow.ToString("o");
                        if (!string.IsNullOrEmpty(comments)) obj["rejectionReason"] = comments;
                    }
                    else if (newStatus.Equals("Pending Approval", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(userEmail)) obj["submittedBy"] = userEmail;
                        obj["submittedAt"] = DateTime.UtcNow.ToString("o");
                    }
                    fullDataJson = obj.ToJsonString();
                }
            }
            catch { }
        }

        // Helper to get and update document status and embedded fullDataJson status
        private async Task<(bool found, int docId, string currentStatus, Action<string, string?, string?, string?, string?> updateStatus)> FindDocumentAsync(string? docType, string? docNumber, int? docId)
        {
            var cleanNum = (docNumber ?? "").Trim().ToLower();
            var normalizedType = (docType ?? "").ToLower().Replace(" ", "").Replace("_", "").Replace("-", "");

            if (normalizedType.Contains("invoice") && !normalizedType.Contains("proforma"))
            {
                var doc = await _context.Invoices.FirstOrDefaultAsync(x => x.InvoiceNumber.ToLower() == cleanNum || (docId.HasValue && x.Id == docId.Value));
                if (doc != null) return (true, doc.Id, doc.Status, (s, email, comms, appSign, appDate) => {
                    doc.Status = s;
                    var fd = doc.FullDataJson;
                    UpdateFullDataJsonStatus(ref fd, s, s, email, comms, appSign, appDate);
                    doc.FullDataJson = fd;
                });
            }
            else if (normalizedType.Contains("quotation"))
            {
                var doc = await _context.Quotations.FirstOrDefaultAsync(x => x.QuotationNumber.ToLower() == cleanNum || (docId.HasValue && x.Id == docId.Value));
                if (doc != null) return (true, doc.Id, doc.Status, (s, email, comms, appSign, appDate) => {
                    doc.Status = s;
                    var fd = doc.FullDataJson;
                    UpdateFullDataJsonStatus(ref fd, s, s, email, comms, appSign, appDate);
                    doc.FullDataJson = fd;
                });
            }
            else if (normalizedType.Contains("purchaseorder") || normalizedType == "po")
            {
                var doc = await _context.PurchaseOrders.FirstOrDefaultAsync(x => x.PONumber.ToLower() == cleanNum || (docId.HasValue && x.Id == docId.Value));
                if (doc != null) return (true, doc.Id, doc.Status, (s, email, comms, appSign, appDate) => {
                    doc.Status = s;
                    var fd = doc.FullDataJson;
                    UpdateFullDataJsonStatus(ref fd, s, s, email, comms, appSign, appDate);
                    doc.FullDataJson = fd;
                });
            }
            else if (normalizedType.Contains("deliverychallan") || normalizedType == "dc" || normalizedType.Contains("challan"))
            {
                var doc = await _context.DeliveryChallans.FirstOrDefaultAsync(x => x.ChallanNumber.ToLower() == cleanNum || (docId.HasValue && x.Id == docId.Value));
                if (doc != null) return (true, doc.Id, doc.Status, (s, email, comms, appSign, appDate) => {
                    doc.Status = s;
                    var fd = doc.FullDataJson;
                    UpdateFullDataJsonStatus(ref fd, s, s, email, comms, appSign, appDate);
                    doc.FullDataJson = fd;
                });
            }
            else if (normalizedType.Contains("proforma"))
            {
                var doc = await _context.ProformaInvoices.FirstOrDefaultAsync(x => x.ProformaNumber.ToLower() == cleanNum || (docId.HasValue && x.Id == docId.Value));
                if (doc != null) return (true, doc.Id, doc.Status, (s, email, comms, appSign, appDate) => {
                    doc.Status = s;
                    var fd = doc.FullDataJson;
                    UpdateFullDataJsonStatus(ref fd, s, s, email, comms, appSign, appDate);
                    doc.FullDataJson = fd;
                });
            }
            else if (normalizedType.Contains("voucher") || normalizedType == "pv")
            {
                var doc = await _context.PaymentVouchers.FirstOrDefaultAsync(x => x.VoucherNumber.ToLower() == cleanNum || (docId.HasValue && x.Id == docId.Value));
                if (doc != null) return (true, doc.Id, doc.Status, (s, email, comms, appSign, appDate) => {
                    doc.Status = s;
                    var fd = doc.FullDataJson;
                    UpdateFullDataJsonStatus(ref fd, s, s, email, comms, appSign, appDate);
                    doc.FullDataJson = fd;
                });
            }

            return (false, 0, string.Empty, (_, _, _, _, _) => { });
        }

        // POST: api/approvals/submit
        [HttpPost("submit")]
        public async Task<IActionResult> SubmitForApproval([FromBody] ApprovalActionDto dto)
        {
            if (string.IsNullOrEmpty(dto.DocumentType) || string.IsNullOrEmpty(dto.DocumentNumber))
            {
                return BadRequest(new { error = "Document type and document number are required" });
            }

            var cleanNum = (dto.DocumentNumber ?? "").Trim().ToLower();
            var normalizedType = (dto.DocumentType ?? "").ToLower().Replace(" ", "").Replace("_", "").Replace("-", "");

            if (normalizedType.Contains("voucher") || normalizedType == "pv")
            {
                var pv = await _context.PaymentVouchers.FirstOrDefaultAsync(x => x.VoucherNumber.ToLower() == cleanNum || (dto.DocumentId.HasValue && x.Id == dto.DocumentId.Value));
                if (pv != null)
                {
                    string recSign = "";
                    string recDate = "";
                    if (!string.IsNullOrEmpty(pv.FullDataJson))
                    {
                        try
                        {
                            var node = JsonNode.Parse(pv.FullDataJson) as JsonObject;
                            if (node != null)
                            {
                                recSign = node["receivedSign"]?.ToString()?.Trim() ?? "";
                                recDate = node["receivedDate"]?.ToString()?.Trim() ?? "";
                            }
                        }
                        catch { }
                    }

                    if (string.IsNullOrEmpty(recSign) || string.IsNullOrEmpty(recDate))
                    {
                        return BadRequest(new { error = "Received By Signature and Date are required before sending for approval." });
                    }
                }
            }

            var (found, resolvedDocId, currentStatus, updateStatus) = await FindDocumentAsync(dto.DocumentType, dto.DocumentNumber, dto.DocumentId);

            if (!found)
            {
                return NotFound(new { error = $"Document '{dto.DocumentNumber}' of type '{dto.DocumentType}' was not found in the database. Please ensure it is saved first." });
            }

            if (currentStatus.Equals("Approved", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "Document has already been approved and cannot be submitted again." });
            }
            if (currentStatus.Equals("Pending Approval", StringComparison.OrdinalIgnoreCase))
            {
                var lastHistory = await _context.ApprovalHistories
                    .Where(h => h.DocumentType.ToLower() == dto.DocumentType.ToLower() && h.DocumentNumber.ToLower() == cleanNum)
                    .OrderByDescending(h => h.Timestamp)
                    .FirstOrDefaultAsync();

                if (lastHistory != null && (lastHistory.Action == "Submitted" || lastHistory.Action == "Resubmitted"))
                {
                    return BadRequest(new { error = "Document is already under review (Pending Approval)." });
                }
            }

            updateStatus("Pending Approval", dto.UserEmail, dto.Comments, null, null);

            bool isResubmission = currentStatus.Equals("Rejected", StringComparison.OrdinalIgnoreCase);
            var actionName = isResubmission ? "Resubmitted" : "Submitted";
            var prevStatus = (currentStatus.Equals("Pending Approval", StringComparison.OrdinalIgnoreCase)) ? "Draft" : currentStatus;

            var history = new ApprovalHistory
            {
                DocumentType = dto.DocumentType,
                DocumentNumber = dto.DocumentNumber,
                DocumentId = dto.DocumentId ?? (resolvedDocId > 0 ? resolvedDocId : null),
                Action = actionName,
                UserEmail = dto.UserEmail,
                UserId = dto.UserId,
                PreviousStatus = string.IsNullOrEmpty(prevStatus) ? "Draft" : prevStatus,
                NewStatus = "Pending Approval",
                Comments = dto.Comments ?? (isResubmission ? "Document resubmitted for approval after correction" : "Submitted for approval"),
                Timestamp = DateTime.UtcNow
            };

            _context.ApprovalHistories.Add(history);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = isResubmission ? "Document resubmitted for approval" : "Document submitted for approval",
                status = "Pending Approval",
                history
            });
        }

        // POST: api/approvals/approve
        [HttpPost("approve")]
        public async Task<IActionResult> ApproveDocument([FromBody] ApprovalActionDto dto)
        {
            var role = GetCallerRole(dto);
            if (!role.Equals("MainAdmin", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(403, new { error = "Unauthorized: Only Main Admin can approve documents." });
            }

            if (string.IsNullOrEmpty(dto.DocumentType) || string.IsNullOrEmpty(dto.DocumentNumber))
            {
                return BadRequest(new { error = "Document type and document number are required" });
            }

            var cleanNum = (dto.DocumentNumber ?? "").Trim().ToLower();
            var normalizedType = (dto.DocumentType ?? "").ToLower().Replace(" ", "").Replace("_", "").Replace("-", "");

            // Apply signature/date requirement ONLY to Payment Voucher
            if (normalizedType.Contains("voucher") || normalizedType == "pv")
            {
                var pv = await _context.PaymentVouchers.FirstOrDefaultAsync(x => x.VoucherNumber.ToLower() == cleanNum || (dto.DocumentId.HasValue && x.Id == dto.DocumentId.Value));
                if (pv != null)
                {
                    string sign = (dto.ApprovedSign ?? "").Trim();
                    string date = (dto.ApprovedDate ?? "").Trim();
                    if (string.IsNullOrEmpty(sign) || string.IsNullOrEmpty(date))
                    {
                        if (!string.IsNullOrEmpty(pv.FullDataJson))
                        {
                            try
                            {
                                var node = JsonNode.Parse(pv.FullDataJson) as JsonObject;
                                if (node != null)
                                {
                                    if (string.IsNullOrEmpty(sign)) sign = node["approvedSign"]?.ToString()?.Trim() ?? "";
                                    if (string.IsNullOrEmpty(date)) date = node["approvedDate"]?.ToString()?.Trim() ?? "";
                                }
                            }
                            catch { }
                        }
                    }

                    if (string.IsNullOrEmpty(sign) || string.IsNullOrEmpty(date))
                    {
                        return BadRequest(new { error = "Approved By Signature and Date are required before approving the payment voucher." });
                    }
                    dto.ApprovedSign = sign;
                    dto.ApprovedDate = date;
                }
            }

            var (found, resolvedDocId, currentStatus, updateStatus) = await FindDocumentAsync(dto.DocumentType, dto.DocumentNumber, dto.DocumentId);

            if (!found)
            {
                return NotFound(new { error = $"Document '{dto.DocumentNumber}' of type '{dto.DocumentType}' was not found in the database." });
            }

            if (currentStatus.Equals("Approved", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "Duplicate Approval Prevented: Document is already approved." });
            }

            updateStatus("Approved", dto.UserEmail, dto.Comments, dto.ApprovedSign, dto.ApprovedDate);

            var history = new ApprovalHistory
            {
                DocumentType = dto.DocumentType,
                DocumentNumber = dto.DocumentNumber,
                DocumentId = dto.DocumentId ?? (resolvedDocId > 0 ? resolvedDocId : null),
                Action = "Approved",
                UserEmail = dto.UserEmail,
                UserId = dto.UserId,
                PreviousStatus = string.IsNullOrEmpty(currentStatus) ? "Pending Approval" : currentStatus,
                NewStatus = "Approved",
                Comments = dto.Comments ?? "Approved by Main Admin",
                Timestamp = DateTime.UtcNow
            };

            _context.ApprovalHistories.Add(history);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Document approved successfully",
                status = "Approved",
                history
            });
        }

        // POST: api/approvals/reject
        [HttpPost("reject")]
        public async Task<IActionResult> RejectDocument([FromBody] ApprovalActionDto dto)
        {
            var role = GetCallerRole(dto);
            if (!role.Equals("MainAdmin", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(403, new { error = "Unauthorized: Only Main Admin can reject documents." });
            }

            var reason = dto.Reason ?? dto.Comments;
            if (string.IsNullOrWhiteSpace(reason))
            {
                return BadRequest(new { error = "A rejection reason is mandatory." });
            }

            if (string.IsNullOrEmpty(dto.DocumentType) || string.IsNullOrEmpty(dto.DocumentNumber))
            {
                return BadRequest(new { error = "Document type and document number are required" });
            }

            var (found, resolvedDocId, currentStatus, updateStatus) = await FindDocumentAsync(dto.DocumentType, dto.DocumentNumber, dto.DocumentId);

            if (!found)
            {
                return NotFound(new { error = $"Document '{dto.DocumentNumber}' of type '{dto.DocumentType}' was not found in the database." });
            }

            if (currentStatus.Equals("Approved", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "Cannot reject an already approved document." });
            }

            if (currentStatus.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "Document is already rejected." });
            }

            updateStatus("Rejected", dto.UserEmail, reason.Trim(), null, null);

            var history = new ApprovalHistory
            {
                DocumentType = dto.DocumentType,
                DocumentNumber = dto.DocumentNumber,
                DocumentId = dto.DocumentId ?? (resolvedDocId > 0 ? resolvedDocId : null),
                Action = "Rejected",
                UserEmail = dto.UserEmail,
                UserId = dto.UserId,
                PreviousStatus = string.IsNullOrEmpty(currentStatus) ? "Pending Approval" : currentStatus,
                NewStatus = "Rejected",
                Comments = reason.Trim(),
                Timestamp = DateTime.UtcNow
            };

            _context.ApprovalHistories.Add(history);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Document rejected",
                status = "Rejected",
                history
            });
        }

        // GET: api/approvals/history/{docType}/{docNumber} or api/approvals/history/{docNumber}
        [HttpGet("history/{docType}/{docNumber}")]
        [HttpGet("history/{docNumber}")]
        public async Task<ActionResult<IEnumerable<ApprovalHistory>>> GetHistory(string? docType, string? docNumber = null)
        {
            var targetNum = docNumber ?? docType ?? string.Empty;
            var cleanNum = targetNum.Trim().ToLower();
            var history = await _context.ApprovalHistories
                .Where(h => h.DocumentNumber.ToLower() == cleanNum)
                .OrderBy(h => h.Timestamp)
                .ToListAsync();

            return Ok(history);
        }

        // GET: api/approvals/pending
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingApprovals()
        {
            var users = await _context.Users.ToDictionaryAsync(u => u.Id, u => u);
            var histories = await _context.ApprovalHistories
                .Where(h => h.Action == "Submitted" || h.Action == "Resubmitted")
                .OrderByDescending(h => h.Timestamp)
                .ToListAsync();

            var pendingInvoices = await _context.Invoices.Include(x => x.Client)
                .Where(x => x.Status == "Pending Approval")
                .Select(x => new { docType = "invoice", docNumber = x.InvoiceNumber, id = x.Id, date = x.Date, dueDate = x.DueDate, amount = x.TotalAmount, billedTo = x.Client != null ? x.Client.Name : "â€”", status = x.Status, fullDataJson = x.FullDataJson, userId = x.UserId })
                .ToListAsync();

            var pendingQuotations = await _context.Quotations.Include(x => x.Client)
                .Where(x => x.Status == "Pending Approval")
                .Select(x => new { docType = "quotation", docNumber = x.QuotationNumber, id = x.Id, date = x.Date, dueDate = x.DueDate, amount = x.TotalAmount, billedTo = x.Client != null ? x.Client.Name : "â€”", status = x.Status, fullDataJson = x.FullDataJson, userId = x.UserId })
                .ToListAsync();

            var pendingPos = await _context.PurchaseOrders.Include(x => x.Client)
                .Where(x => x.Status == "Pending Approval")
                .Select(x => new { docType = "purchase_order", docNumber = x.PONumber, id = x.Id, date = x.Date, dueDate = x.DueDate, amount = x.TotalAmount, billedTo = x.Client != null ? x.Client.Name : "â€”", status = x.Status, fullDataJson = x.FullDataJson, userId = x.UserId })
                .ToListAsync();

            var pendingDcs = await _context.DeliveryChallans.Include(x => x.Client)
                .Where(x => x.Status == "Pending Approval")
                .Select(x => new { docType = "delivery_challan", docNumber = x.ChallanNumber, id = x.Id, date = x.Date, dueDate = x.DueDate, amount = x.TotalAmount, billedTo = x.Client != null ? x.Client.Name : "â€”", status = x.Status, fullDataJson = x.FullDataJson, userId = x.UserId })
                .ToListAsync();

            var pendingPis = await _context.ProformaInvoices.Include(x => x.Client)
                .Where(x => x.Status == "Pending Approval")
                .Select(x => new { docType = "proforma_invoice", docNumber = x.ProformaNumber, id = x.Id, date = x.Date, dueDate = x.DueDate, amount = x.TotalAmount, billedTo = x.Client != null ? x.Client.Name : "â€”", status = x.Status, fullDataJson = x.FullDataJson, userId = x.UserId })
                .ToListAsync();

            var pendingPvs = await _context.PaymentVouchers.Include(x => x.Client)
                .Where(x => x.Status == "Pending Approval")
                .Select(x => new { docType = "payment_voucher", docNumber = x.VoucherNumber, id = x.Id, date = x.Date, dueDate = x.Date, amount = x.Amount, billedTo = x.Client != null ? x.Client.Name : "â€”", status = x.Status, fullDataJson = x.FullDataJson, userId = x.UserId })
                .ToListAsync();

            var rawItems = pendingInvoices
                .Concat(pendingQuotations)
                .Concat(pendingPos)
                .Concat(pendingDcs)
                .Concat(pendingPis)
                .Concat(pendingPvs)
                .ToList();

            var allPending = rawItems.Select(item =>
            {
                string submitterEmail = "User";
                string submitterName = "User";
                DateTime? submittedAt = null;

                if (item.userId.HasValue && users.TryGetValue(item.userId.Value, out var u))
                {
                    submitterEmail = u.Email;
                    submitterName = !string.IsNullOrEmpty(u.FullName) ? u.FullName : u.Username;
                }

                var hist = histories.FirstOrDefault(h => h.DocumentNumber.Equals(item.docNumber, StringComparison.OrdinalIgnoreCase));
                if (hist != null)
                {
                    if (submitterEmail == "User" && !string.IsNullOrEmpty(hist.UserEmail))
                    {
                        submitterEmail = hist.UserEmail;
                        submitterName = hist.UserEmail.Split('@')[0];
                    }
                    submittedAt = hist.Timestamp;
                }

                return new
                {
                    item.docType,
                    item.docNumber,
                    item.id,
                    date = item.date.ToString("yyyy-MM-dd"),
                    dueDate = item.dueDate.ToString("yyyy-MM-dd"),
                    item.amount,
                    item.billedTo,
                    item.status,
                    item.fullDataJson,
                    submittedBy = submitterEmail,
                    submittedByName = submitterName,
                    submittedAt = submittedAt?.ToString("o") ?? item.date.ToString("o")
                };
            }).ToList();

            return Ok(new { count = allPending.Count, items = allPending });
        }
    }
}

