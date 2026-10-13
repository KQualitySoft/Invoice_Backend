using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoiceGen.Data;
using InvoiceGen.Models;
using InvoiceGen.Services;

namespace InvoiceGen.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;

        public UsersController(AppDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public class CreateUserDto
        {
            public string Email { get; set; } = string.Empty;
            public string? Username { get; set; }
            public string? FullName { get; set; }
            public string Password { get; set; } = string.Empty;
            public string? Role { get; set; }
            public bool IsActive { get; set; } = true;
            public string? PrivilegesJson { get; set; }
        }

        public class UpdateUserDto
        {
            public string? FullName { get; set; }
            public string? Role { get; set; }
            public bool? IsActive { get; set; }
            public string? PrivilegesJson { get; set; }
        }

        public class ResetPasswordDto
        {
            public string NewPassword { get; set; } = string.Empty;
        }

        // GET: api/Users
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetUsers()
        {
            var users = await _context.Users
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.Username,
                    u.FullName,
                    u.Role,
                    u.IsActive,
                    u.PrivilegesJson,
                    u.CreatedAt,
                    u.LastLoginAt
                })
                .ToListAsync();

            return Ok(users);
        }

        // GET: api/Users/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetUser(int id)
        {
            var user = await _context.Users
                .Where(u => u.Id == id)
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.Username,
                    u.FullName,
                    u.Role,
                    u.IsActive,
                    u.PrivilegesJson,
                    u.CreatedAt,
                    u.LastLoginAt
                })
                .FirstOrDefaultAsync();

            if (user == null) return NotFound();
            return Ok(user);
        }

        // POST: api/Users
        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
        {
            if (string.IsNullOrEmpty(dto.Email) || string.IsNullOrEmpty(dto.Password))
            {
                return BadRequest(new { error = "Email and password are required" });
            }

            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == dto.Email.ToLower()))
            {
                return BadRequest(new { error = "User with this email already exists" });
            }

            var user = new User
            {
                Email = dto.Email,
                Username = dto.Username ?? dto.Email.Split('@')[0],
                FullName = dto.FullName ?? dto.Email.Split('@')[0],
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = dto.Role ?? "User",
                IsActive = dto.IsActive,
                PrivilegesJson = dto.PrivilegesJson,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Automatically send welcome email with credentials to registered email
            try
            {
                await _emailService.SendUserWelcomeEmailAsync(
                    user.Email,
                    user.FullName,
                    user.Username,
                    dto.Password,
                    user.Role
                );
            }
            catch (Exception ex)
            {
                // Never log password; log delivery note
                Console.WriteLine($"[Email] Failed to send welcome email to {user.Email}: {ex.Message}");
            }

            return Ok(new
            {
                user.Id,
                user.Email,
                user.Username,
                user.FullName,
                user.Role,
                user.IsActive,
                user.PrivilegesJson
            });
        }

        // PUT: api/Users/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto dto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (!string.IsNullOrEmpty(dto.FullName)) user.FullName = dto.FullName;
            if (!string.IsNullOrEmpty(dto.Role)) user.Role = dto.Role;
            if (dto.IsActive.HasValue) user.IsActive = dto.IsActive.Value;
            if (dto.PrivilegesJson != null) user.PrivilegesJson = dto.PrivilegesJson;

            await _context.SaveChangesAsync();
            return Ok(new { message = "User updated successfully" });
        }

        // POST: api/Users/5/toggle-status
        [HttpPost("{id}/toggle-status")]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.IsActive = !user.IsActive;
            await _context.SaveChangesAsync();

            return Ok(new { user.Id, user.IsActive });
        }

        // POST: api/Users/5/reset-password
        [HttpPost("{id}/reset-password")]
        public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordDto dto)
        {
            if (string.IsNullOrEmpty(dto.NewPassword) || dto.NewPassword.Length < 4)
            {
                return BadRequest(new { error = "Password must be at least 4 characters long" });
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Password reset successfully" });
        }

        // DELETE: api/Users/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (user.Role == "MainAdmin" || id == 1)
            {
                return BadRequest(new { error = "Cannot delete Main Admin user" });
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return Ok(new { message = "User deleted successfully" });
        }
    }
}
