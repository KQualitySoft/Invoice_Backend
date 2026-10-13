using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoiceGen.Data;

namespace InvoiceGen.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ReportsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Reports/summary
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] int? userId)
        {
            var invoicesQuery = _context.Invoices.AsQueryable();
            var quotationsQuery = _context.Quotations.AsQueryable();
            var posQuery = _context.PurchaseOrders.AsQueryable();
            var dcsQuery = _context.DeliveryChallans.AsQueryable();
            var pisQuery = _context.ProformaInvoices.AsQueryable();
            var pvsQuery = _context.PaymentVouchers.AsQueryable();

            if (userId.HasValue)
            {
                invoicesQuery = invoicesQuery.Where(x => x.UserId == userId.Value);
                quotationsQuery = quotationsQuery.Where(x => x.UserId == userId.Value);
                posQuery = posQuery.Where(x => x.UserId == userId.Value);
                dcsQuery = dcsQuery.Where(x => x.UserId == userId.Value);
                pisQuery = pisQuery.Where(x => x.UserId == userId.Value);
                pvsQuery = pvsQuery.Where(x => x.UserId == userId.Value);
            }

            var invAmounts = await invoicesQuery.Select(x => x.TotalAmount).ToListAsync();
            var qtnAmounts = await quotationsQuery.Select(x => x.TotalAmount).ToListAsync();
            var poAmounts = await posQuery.Select(x => x.TotalAmount).ToListAsync();
            var dcAmounts = await dcsQuery.Select(x => x.TotalAmount).ToListAsync();
            var piAmounts = await pisQuery.Select(x => x.TotalAmount).ToListAsync();
            var pvAmounts = await pvsQuery.Select(x => x.Amount).ToListAsync();

            return Ok(new
            {
                invoices = new { count = invAmounts.Count, total = invAmounts.Sum() },
                quotations = new { count = qtnAmounts.Count, total = qtnAmounts.Sum() },
                purchaseOrders = new { count = poAmounts.Count, total = poAmounts.Sum() },
                deliveryChallans = new { count = dcAmounts.Count, total = dcAmounts.Sum() },
                proformas = new { count = piAmounts.Count, total = piAmounts.Sum() },
                paymentVouchers = new { count = pvAmounts.Count, total = pvAmounts.Sum() },
                generatedAt = DateTime.UtcNow
            });
        }
    }
}
