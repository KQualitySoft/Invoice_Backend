using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using InvoiceGen.Data;
using InvoiceGen.Models;
using InvoiceGen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using BCrypt.Net;

namespace InvoiceGen.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly IEmailService _emailService;

        public AuthController(AppDbContext context, IConfiguration config, IEmailService emailService)
        {
            _context = context;
            _config = config;
            _emailService = emailService;
        }

        public class SignupRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        [HttpPost("signup")]
        public async Task<IActionResult> Signup([FromBody] SignupRequest req)
        {
            if (string.IsNullOrEmpty(req.Email) || string.IsNullOrEmpty(req.Password))
                return BadRequest(new { error = "Email and password are required" });

            if (await _context.Users.AnyAsync(u => u.Email == req.Email))
                return BadRequest(new { error = "User already exists" });

            var user = new User
            {
                Email = req.Email,
                Username = req.Email.Split('@')[0],
                FullName = req.Email.Split('@')[0],
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "User created successfully" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] SignupRequest req)
        {
            if (string.IsNullOrEmpty(req.Email) || string.IsNullOrEmpty(req.Password))
                return BadRequest(new { error = "Email and password are required" });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == req.Email || u.Username == req.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
                return Unauthorized(new { error = "Invalid email or password" });

            if (!user.IsActive)
                return Unauthorized(new { error = "Your account is deactivated. Please contact Main Admin." });

            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Generate JWT Token
            string tokenString = string.Empty;
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"] ?? "QualitySalesPro_InvoiceGen_SuperSecret_Jwt_Key_2026_Secure!");
                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim(ClaimTypes.Email, user.Email),
                        new Claim(ClaimTypes.Role, user.Role),
                        new Claim("username", user.Username)
                    }),
                    Expires = DateTime.UtcNow.AddDays(7),
                    Issuer = _config["Jwt:Issuer"] ?? "InvoiceGenAPI",
                    Audience = _config["Jwt:Audience"] ?? "InvoiceGenClient",
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                };
                var token = tokenHandler.CreateToken(tokenDescriptor);
                tokenString = tokenHandler.WriteToken(token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating token: {ex.Message}");
            }

            return Ok(new
            {
                message = "Login successful",
                token = tokenString,
                userId = user.Id,
                email = user.Email,
                username = user.Username,
                fullName = user.FullName,
                role = user.Role,
                privilegesJson = user.PrivilegesJson
            });
        }

        public class UpdateRequest
        {
            public string Email { get; set; } = string.Empty;
            public string? NewEmail { get; set; }
            public string? NewPassword { get; set; }
        }

        [HttpPut("update")]
        public async Task<IActionResult> Update([FromBody] UpdateRequest req)
        {
            if (string.IsNullOrEmpty(req.Email))
                return BadRequest(new { error = "Current email is required" });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
            if (user == null)
                return NotFound(new { error = "User not found" });

            bool updated = false;

            if (!string.IsNullOrEmpty(req.NewEmail))
            {
                user.Email = req.NewEmail;
                updated = true;
            }

            if (!string.IsNullOrEmpty(req.NewPassword))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
                updated = true;
            }

            if (!updated)
                return BadRequest(new { error = "No new details provided to update" });

            await _context.SaveChangesAsync();
            return Ok(new { message = "Credentials updated successfully" });
        }

        public class ForgotPasswordRequest
        {
            public string Email { get; set; } = string.Empty;
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email))
                return BadRequest(new { error = "Email address is required" });

            var cleanEmail = req.Email.Trim().ToLower();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == cleanEmail || u.Username.ToLower() == cleanEmail);

            if (user == null || !user.IsActive)
            {
                // Consistent response to prevent email enumeration
                return Ok(new { message = "If the account exists, a password reset link has been sent to your registered email address." });
            }

            // Generate cryptographically secure random token (64 hex characters)
            var tokenBytes = RandomNumberGenerator.GetBytes(32);
            var token = Convert.ToHexString(tokenBytes).ToLower();

            user.ResetToken = token;
            user.ResetTokenExpiry = DateTime.UtcNow.AddHours(24);
            await _context.SaveChangesAsync();

            try
            {
                await _emailService.SendPasswordResetEmailAsync(
                    user.Email,
                    user.FullName,
                    token
                );
            }
            catch (Exception ex)
            {
                // Never log password
                Console.WriteLine($"[Email] Failed to send password reset email to {user.Email}: {ex.Message}");
                return StatusCode(500, new { error = "Could not deliver password reset email. Please verify SMTP settings or try again later." });
            }

            return Ok(new { message = "If the account exists, a password reset link has been sent to your registered email address." });
        }

        [HttpGet("verify-reset-token")]
        public async Task<IActionResult> VerifyResetToken([FromQuery] string token, [FromQuery] string? email)
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest(new { valid = false, error = "Token is required" });

            var cleanToken = token.Trim();
            var cleanEmail = (email ?? string.Empty).Trim().ToLower();

            var user = await _context.Users.FirstOrDefaultAsync(u =>
                (string.IsNullOrEmpty(cleanEmail) || u.Email.ToLower() == cleanEmail) &&
                u.ResetToken == cleanToken);

            if (user == null || user.ResetTokenExpiry == null || user.ResetTokenExpiry < DateTime.UtcNow)
            {
                return Ok(new { valid = false, error = "Invalid or expired password reset link. Please request a new one." });
            }

            return Ok(new { valid = true, email = user.Email });
        }

        public class ResetPasswordRequest
        {
            public string? Email { get; set; }
            public string Token { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Token) || string.IsNullOrWhiteSpace(req.NewPassword))
                return BadRequest(new { error = "Token and new password are required" });

            if (req.NewPassword.Length < 4)
                return BadRequest(new { error = "Password must be at least 4 characters long" });

            var cleanToken = req.Token.Trim();
            var cleanEmail = (req.Email ?? string.Empty).Trim().ToLower();

            var user = await _context.Users.FirstOrDefaultAsync(u =>
                (string.IsNullOrEmpty(cleanEmail) || u.Email.ToLower() == cleanEmail) &&
                u.ResetToken == cleanToken);

            if (user == null || user.ResetTokenExpiry == null || user.ResetTokenExpiry < DateTime.UtcNow)
            {
                return BadRequest(new { error = "Invalid or expired password reset link. Please request a new one." });
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
            user.ResetToken = null;
            user.ResetTokenExpiry = null;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Password has been reset successfully. You can now log in with your new password." });
        }
    }
}
