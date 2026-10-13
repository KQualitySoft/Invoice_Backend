using InvoiceGen.Data;
using InvoiceGen.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGen.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClientsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ClientsController(AppDbContext context)
        {
            _context = context;
        }

        // DTO for creating/updating clients from the frontend
        public class ClientDto
        {
            public string Name { get; set; } = string.Empty;
            public string? Gstin { get; set; }
            public string? Email { get; set; }
            public string? Phone { get; set; }
            public string? Address { get; set; }
            public string? City { get; set; }
            public string? State { get; set; }
            public string? Postal { get; set; }
            public string? Country { get; set; }
            public int? UserId { get; set; }
        }

        // GET: api/Clients
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Client>>> GetClients([FromQuery] int? userId)
        {
            var allClients = await _context.Clients.ToListAsync();

            // Track unique clients and duplicate entities to prune from DB
            var uniqueList = new List<Client>();
            var duplicatesToRemove = new List<Client>();

            foreach (var c in allClients.OrderBy(x => x.CreatedAt))
            {
                var cName = (c.Name ?? "").Trim().ToLower();
                var cGstin = (c.Gstin ?? "").Trim().ToUpper();

                // Find if an existing client in uniqueList matches by GSTIN or by Name
                var match = uniqueList.FirstOrDefault(u =>
                    (!string.IsNullOrEmpty(cGstin) && !string.IsNullOrEmpty(u.Gstin) && u.Gstin.Trim().ToUpper() == cGstin) ||
                    (!string.IsNullOrEmpty(cName) && (u.Name ?? "").Trim().ToLower() == cName)
                );

                if (match != null)
                {
                    // Merge fields into match if match is missing them
                    if (string.IsNullOrEmpty(match.Gstin) && !string.IsNullOrEmpty(c.Gstin)) match.Gstin = c.Gstin;
                    if (string.IsNullOrEmpty(match.Email) && !string.IsNullOrEmpty(c.Email)) match.Email = c.Email;
                    if (string.IsNullOrEmpty(match.Phone) && !string.IsNullOrEmpty(c.Phone)) match.Phone = c.Phone;
                    if (string.IsNullOrEmpty(match.Address) && !string.IsNullOrEmpty(c.Address)) match.Address = c.Address;
                    if (string.IsNullOrEmpty(match.City) && !string.IsNullOrEmpty(c.City)) match.City = c.City;
                    if (string.IsNullOrEmpty(match.State) && !string.IsNullOrEmpty(c.State)) match.State = c.State;
                    if (string.IsNullOrEmpty(match.Postal) && !string.IsNullOrEmpty(c.Postal)) match.Postal = c.Postal;
                    if (string.IsNullOrEmpty(match.Country) && !string.IsNullOrEmpty(c.Country)) match.Country = c.Country;
                    duplicatesToRemove.Add(c);
                }
                else
                {
                    uniqueList.Add(c);
                }
            }

            if (duplicatesToRemove.Count > 0)
            {
                foreach (var dup in duplicatesToRemove)
                {
                    var dupName = (dup.Name ?? "").Trim().ToLower();
                    var dupGst = (dup.Gstin ?? "").Trim().ToUpper();
                    var primary = uniqueList.FirstOrDefault(u =>
                        (!string.IsNullOrEmpty(dupGst) && !string.IsNullOrEmpty(u.Gstin) && u.Gstin.Trim().ToUpper() == dupGst) ||
                        (!string.IsNullOrEmpty(dupName) && (u.Name ?? "").Trim().ToLower() == dupName)
                    );

                    if (primary != null)
                    {
                        var invoices = await _context.Invoices.Where(i => i.ClientId == dup.Id).ToListAsync();
                        foreach (var inv in invoices) inv.ClientId = primary.Id;

                        var quotations = await _context.Quotations.Where(q => q.ClientId == dup.Id).ToListAsync();
                        foreach (var q in quotations) q.ClientId = primary.Id;

                        var pos = await _context.PurchaseOrders.Where(p => p.ClientId == dup.Id).ToListAsync();
                        foreach (var p in pos) p.ClientId = primary.Id;

                        var dcs = await _context.DeliveryChallans.Where(d => d.ClientId == dup.Id).ToListAsync();
                        foreach (var d in dcs) d.ClientId = primary.Id;

                        var pis = await _context.ProformaInvoices.Where(p => p.ClientId == dup.Id).ToListAsync();
                        foreach (var p in pis) p.ClientId = primary.Id;
                    }
                }

                _context.Clients.RemoveRange(duplicatesToRemove);
                await _context.SaveChangesAsync();
            }

            if (userId.HasValue)
            {
                return Ok(uniqueList.Where(c => c.UserId == userId.Value).ToList());
            }

            return Ok(uniqueList);
        }

        // POST: api/Clients  — upsert by name or GSTIN to guarantee one client is only saved once
        [HttpPost]
        public async Task<ActionResult<Client>> PostClient([FromBody] ClientDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { error = "Client name is required." });

            var nameKey = dto.Name.Trim().ToLower();

            // Try to find existing client by GSTIN first, then by name (system-wide check)
            Client? existing = null;
            if (!string.IsNullOrEmpty(dto.Gstin))
            {
                var gstinKey = dto.Gstin.Trim().ToUpper();
                existing = await _context.Clients.FirstOrDefaultAsync(c =>
                    c.Gstin != null && c.Gstin.ToUpper() == gstinKey);
            }
            if (existing == null)
            {
                existing = await _context.Clients.FirstOrDefaultAsync(c =>
                    c.Name.ToLower() == nameKey);
            }

            if (existing != null)
            {
                // Update existing client with any new info
                existing.Name = dto.Name.Trim();
                if (!string.IsNullOrEmpty(dto.Gstin)) existing.Gstin = dto.Gstin.Trim();
                if (!string.IsNullOrEmpty(dto.Email)) existing.Email = dto.Email.Trim();
                if (!string.IsNullOrEmpty(dto.Phone)) existing.Phone = dto.Phone.Trim();
                if (!string.IsNullOrEmpty(dto.Address)) existing.Address = dto.Address.Trim();
                if (!string.IsNullOrEmpty(dto.City)) existing.City = dto.City.Trim();
                if (!string.IsNullOrEmpty(dto.State)) existing.State = dto.State.Trim();
                if (!string.IsNullOrEmpty(dto.Postal)) existing.Postal = dto.Postal.Trim();
                if (!string.IsNullOrEmpty(dto.Country)) existing.Country = dto.Country.Trim();
                await _context.SaveChangesAsync();
                return Ok(existing);
            }

            var client = new Client
            {
                Name = dto.Name.Trim(),
                Gstin = dto.Gstin?.Trim(),
                Email = dto.Email?.Trim(),
                Phone = dto.Phone?.Trim(),
                Address = dto.Address?.Trim(),
                City = dto.City?.Trim(),
                State = dto.State?.Trim(),
                Postal = dto.Postal?.Trim(),
                Country = dto.Country?.Trim() ?? "India",
                UserId = dto.UserId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Clients.Add(client);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetClient), new { id = client.Id }, client);
        }

        // GET: api/Clients/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Client>> GetClient(int id)
        {
            var client = await _context.Clients.FindAsync(id);

            if (client == null)
            {
                return NotFound();
            }

            return client;
        }

        // GET: api/Clients/5/dashboard
        [HttpGet("{id}/dashboard")]
        public async Task<IActionResult> GetClientDashboard(int id)
        {
            var client = await _context.Clients
                .Include(c => c.Quotations)
                .Include(c => c.PurchaseOrders)
                .Include(c => c.DeliveryChallans)
                .Include(c => c.ProformaInvoices)
                .Include(c => c.Invoices)
                .Include(c => c.PaymentVouchers)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (client == null)
            {
                return NotFound(new { error = "Client not found" });
            }

            var dashboardData = new
            {
                ClientId = client.Id,
                ClientName = client.Name,
                TotalQuotations = client.Quotations.Count,
                TotalInvoices = client.Invoices.Count,
                Invoices = client.Invoices,
                Quotations = client.Quotations,
                PurchaseOrders = client.PurchaseOrders,
                DeliveryChallans = client.DeliveryChallans,
                ProformaInvoices = client.ProformaInvoices,
                PaymentVouchers = client.PaymentVouchers
            };

            return Ok(dashboardData);
        }
    }
}
