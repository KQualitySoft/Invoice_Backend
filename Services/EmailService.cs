using System;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace InvoiceGen.Services
{
    public interface IEmailService
    {
        Task SendUserWelcomeEmailAsync(string recipientEmail, string fullName, string username, string initialPassword, string role);
        Task SendPasswordResetEmailAsync(string recipientEmail, string fullName, string resetToken);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendUserWelcomeEmailAsync(string recipientEmail, string fullName, string username, string initialPassword, string role)
        {
            var frontendUrl = _config["App:FrontendUrl"] ?? "http://localhost:5173";
            var loginUrl = $"{frontendUrl.TrimEnd('/')}/login";
            var subject = "Welcome to Quality SalesPro - Your Account Credentials";

            var html = $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset=""utf-8"">
  <style>
    body {{ font-family: 'Segoe UI', Arial, sans-serif; background-color: #f8fafc; margin: 0; padding: 24px; color: #1e293b; }}
    .container {{ max-width: 580px; margin: 0 auto; background: #ffffff; border-radius: 12px; border: 1px solid #e2e8f0; overflow: hidden; }}
    .header {{ background: linear-gradient(135deg, #7b3fe4, #6366f1); padding: 32px 28px; text-align: center; color: #ffffff; }}
    .header h1 {{ margin: 0; font-size: 22px; font-weight: 700; letter-spacing: -0.02em; }}
    .header p {{ margin: 6px 0 0; font-size: 14px; opacity: 0.9; }}
    .content {{ padding: 32px 28px; }}
    .greeting {{ font-size: 16px; font-weight: 600; margin-bottom: 14px; }}
    .intro {{ font-size: 14px; line-height: 1.6; color: #475569; margin-bottom: 22px; }}
    .creds-box {{ background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 20px; margin-bottom: 24px; }}
    .cred-row {{ display: flex; justify-content: space-between; padding: 8px 0; border-bottom: 1px solid #edf2f7; font-size: 13.5px; }}
    .cred-row:last-child {{ border-bottom: none; }}
    .cred-label {{ color: #64748b; font-weight: 500; }}
    .cred-val {{ color: #0f172a; font-weight: 700; word-break: break-all; }}
    .btn-wrap {{ text-align: center; margin: 28px 0; }}
    .btn {{ display: inline-block; background: #7b3fe4; color: #ffffff !important; text-decoration: none; padding: 12px 28px; border-radius: 6px; font-weight: 600; font-size: 14px; }}
    .security-note {{ background: #fefce8; border: 1px solid #fef08a; border-radius: 6px; padding: 12px 14px; font-size: 12.5px; color: #854d0e; line-height: 1.5; }}
    .footer {{ background: #f1f5f9; padding: 18px 28px; font-size: 12px; color: #94a3b8; text-align: center; }}
  </style>
</head>
<body>
  <div class=""container"">
    <div class=""header"">
      <h1>Quality SalesPro</h1>
      <p>Account Access Information</p>
    </div>
    <div class=""content"">
      <div class=""greeting"">Hello {fullName},</div>
      <p class=""intro"">
        An administrator has created an account for you on <strong>Quality SalesPro</strong>. You can now log in using the credentials below:
      </p>

      <div class=""creds-box"">
        <div class=""cred-row"">
          <span class=""cred-label"">Portal URL:</span>
          <span class=""cred-val"">{loginUrl}</span>
        </div>
        <div class=""cred-row"">
          <span class=""cred-label"">Registered Email:</span>
          <span class=""cred-val"">{recipientEmail}</span>
        </div>
        <div class=""cred-row"">
          <span class=""cred-label"">Username:</span>
          <span class=""cred-val"">{username}</span>
        </div>
        <div class=""cred-row"">
          <span class=""cred-label"">Initial Password:</span>
          <span class=""cred-val"">{initialPassword}</span>
        </div>
        <div class=""cred-row"">
          <span class=""cred-label"">Role:</span>
          <span class=""cred-val"">{role}</span>
        </div>
      </div>

      <div class=""btn-wrap"">
        <a href=""{loginUrl}"" class=""btn"">Log In to Quality SalesPro</a>
      </div>

      <div class=""security-note"">
        <strong>Security Recommendation:</strong> For your security, please log in and update your password immediately upon your first sign in.
      </div>
    </div>
    <div class=""footer"">
      &copy; {DateTime.UtcNow.Year} Quality SalesPro. All rights reserved.<br>
      This is an automated notification. Please do not reply directly to this email.
    </div>
  </div>
</body>
</html>";

            var plainText = $@"
Hello {fullName},

An administrator has created an account for you on Quality SalesPro.

Login Credentials:
Portal URL: {loginUrl}
Registered Email: {recipientEmail}
Username: {username}
Initial Password: {initialPassword}
Role: {role}

Please log in at {loginUrl} and update your password upon your first sign in.

Quality SalesPro
";

            await SendEmailInternalAsync(recipientEmail, fullName, subject, html, plainText);
        }

        public async Task SendPasswordResetEmailAsync(string recipientEmail, string fullName, string resetToken)
        {
            var frontendUrl = _config["App:FrontendUrl"] ?? "http://localhost:5173";
            var resetUrl = $"{frontendUrl.TrimEnd('/')}/reset-password?token={resetToken}&email={Uri.EscapeDataString(recipientEmail)}";
            var subject = "Quality SalesPro - Password Reset Request";

            var html = $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset=""utf-8"">
  <style>
    body {{ font-family: 'Segoe UI', Arial, sans-serif; background-color: #f8fafc; margin: 0; padding: 24px; color: #1e293b; }}
    .container {{ max-width: 580px; margin: 0 auto; background: #ffffff; border-radius: 12px; border: 1px solid #e2e8f0; overflow: hidden; }}
    .header {{ background: linear-gradient(135deg, #7b3fe4, #6366f1); padding: 32px 28px; text-align: center; color: #ffffff; }}
    .header h1 {{ margin: 0; font-size: 22px; font-weight: 700; letter-spacing: -0.02em; }}
    .header p {{ margin: 6px 0 0; font-size: 14px; opacity: 0.9; }}
    .content {{ padding: 32px 28px; }}
    .greeting {{ font-size: 16px; font-weight: 600; margin-bottom: 14px; }}
    .intro {{ font-size: 14px; line-height: 1.6; color: #475569; margin-bottom: 24px; }}
    .btn-wrap {{ text-align: center; margin: 28px 0; }}
    .btn {{ display: inline-block; background: #7b3fe4; color: #ffffff !important; text-decoration: none; padding: 12px 32px; border-radius: 6px; font-weight: 600; font-size: 14px; }}
    .link-box {{ background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; padding: 12px 14px; font-size: 12px; color: #64748b; word-break: break-all; margin: 20px 0; }}
    .security-note {{ background: #f1f5f9; border-left: 4px solid #94a3b8; padding: 12px 14px; font-size: 12.5px; color: #64748b; line-height: 1.5; }}
    .footer {{ background: #f1f5f9; padding: 18px 28px; font-size: 12px; color: #94a3b8; text-align: center; }}
  </style>
</head>
<body>
  <div class=""container"">
    <div class=""header"">
      <h1>Quality SalesPro</h1>
      <p>Password Reset Request</p>
    </div>
    <div class=""content"">
      <div class=""greeting"">Hello {fullName},</div>
      <p class=""intro"">
        We received a request to reset your Quality SalesPro account password. Click the button below to securely choose a new password:
      </p>

      <div class=""btn-wrap"">
        <a href=""{resetUrl}"" class=""btn"">Reset Password</a>
      </div>

      <p style=""font-size: 13px; color: #64748b; margin-bottom: 6px;"">
        Or copy and paste this link into your web browser:
      </p>
      <div class=""link-box"">
        {resetUrl}
      </div>

      <div class=""security-note"">
        This link will expire in 24 hours. If you did not request this password reset, you can safely ignore this email; your existing password will remain secure and unchanged.
      </div>
    </div>
    <div class=""footer"">
      &copy; {DateTime.UtcNow.Year} Quality SalesPro. All rights reserved.<br>
      This is an automated notification. Please do not reply directly to this email.
    </div>
  </div>
</body>
</html>";

            var plainText = $@"
Hello {fullName},

We received a request to reset the password for your Quality SalesPro account.

Please visit the link below to set your new password:
{resetUrl}

This link is valid for 24 hours. If you did not make this request, please ignore this email.

Quality SalesPro
";

            await SendEmailInternalAsync(recipientEmail, fullName, subject, html, plainText);
        }

        private async Task SendEmailInternalAsync(string recipientEmail, string recipientName, string subject, string htmlBody, string plainTextBody)
        {
            var host = _config["Smtp:Server"] ?? "mail.kqualitysoft.com";
            var port = int.TryParse(_config["Smtp:Port"], out var p) ? p : 465;
            var fromEmail = _config["Smtp:SenderEmail"] ?? "qualityapplication@kqualitysoft.com";
            var fromName = _config["Smtp:SenderName"] ?? "Quality SalesPro";
            var username = _config["Smtp:Username"] ?? "qualityapplication@kqualitysoft.com";

            // Securely retrieve SMTP password from configuration or environment variable
            var password = _config["Smtp:Password"]
                           ?? Environment.GetEnvironmentVariable("SMTP_PASSWORD")
                           ?? Environment.GetEnvironmentVariable("SMTP_SECRET")
                           ?? string.Empty;

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(new MailboxAddress(string.IsNullOrWhiteSpace(recipientName) ? recipientEmail : recipientName, recipientEmail));
            message.Subject = subject;

            var builder = new BodyBuilder
            {
                HtmlBody = htmlBody,
                TextBody = plainTextBody
            };
            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();

            // Support SSL validation on port 465
            client.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

            try
            {
                // Connect via implicit SSL/TLS on port 465
                await client.ConnectAsync(host, port, SecureSocketOptions.SslOnConnect);

                if (!string.IsNullOrEmpty(password))
                {
                    await client.AuthenticateAsync(username, password);
                }

                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("Email dispatched successfully to {RecipientEmail} with subject: {Subject}", recipientEmail, subject);
            }
            catch (Exception ex)
            {
                // Do not log or expose the SMTP password
                _logger.LogError("Failed to deliver email to {RecipientEmail}: {ErrorMessage}", recipientEmail, ex.Message);
                throw;
            }
        }
    }
}
