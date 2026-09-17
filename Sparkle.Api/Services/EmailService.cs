using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace Sparkle.Api.Services;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body, bool isHtml = true);
    Task SendOrderConfirmationAsync(string to, string orderNumber, decimal totalAmount);
    Task SendOrderStatusUpdateAsync(string to, string orderNumber, string status);
    Task SendTicketResponseAsync(string to, string ticketNumber, string message);
    Task SendRefundApprovedAsync(string to, string orderNumber, decimal refundAmount);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpUsername;
    private readonly string _smtpPassword;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly bool _enableSsl;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        _smtpHost = configuration["Email:SmtpHost"] ?? "smtp.gmail.com";
        _smtpPort = int.Parse(configuration["Email:SmtpPort"] ?? "587");
        _smtpUsername = configuration["Email:Username"] ?? "";
        _smtpPassword = configuration["Email:Password"] ?? "";
        _fromEmail = configuration["Email:FromEmail"] ?? "noreply@sparkle.com";
        _fromName = configuration["Email:FromName"] ?? "Sparkle";
        _enableSsl = bool.Parse(configuration["Email:EnableSsl"] ?? "true");
    }

    public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = true)
    {
        try
        {
            if (string.IsNullOrEmpty(_smtpUsername) || string.IsNullOrEmpty(_smtpPassword))
            {
                _logger.LogWarning("SMTP credentials not configured. Skipping email sending.");
                return;
            }

            using var message = new MailMessage();
            message.From = new MailAddress(_fromEmail, _fromName);
            message.To.Add(new MailAddress(to));
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = isHtml;

            using var smtpClient = new SmtpClient(_smtpHost, _smtpPort);
            smtpClient.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);
            smtpClient.EnableSsl = _enableSsl;

            await smtpClient.SendMailAsync(message);
            _logger.LogInformation("Email sent successfully to {To}", to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", to);
            throw;
        }
    }

    public async Task SendOrderConfirmationAsync(string to, string orderNumber, decimal totalAmount)
    {
        var subject = $"Order Confirmation - {orderNumber}";
        var body = $@"
            <html>
            <head>
                <style>
                    body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                    .header {{ background: #4f46e5; color: white; padding: 20px; text-align: center; }}
                    .content {{ background: #f9fafb; padding: 20px; }}
                    .order-details {{ background: white; padding: 15px; border-radius: 8px; margin: 20px 0; }}
                    .footer {{ text-align: center; padding: 20px; color: #6b7280; font-size: 12px; }}
                    .button {{ display: inline-block; background: #4f46e5; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <div class='header'>
                        <h1>Order Confirmed!</h1>
                    </div>
                    <div class='content'>
                        <p>Thank you for your order!</p>
                        <div class='order-details'>
                            <h3>Order Details</h3>
                            <p><strong>Order Number:</strong> {orderNumber}</p>
                            <p><strong>Total Amount:</strong> ৳{totalAmount:N2}</p>
                        </div>
                        <p style='text-align: center;'>
                            <a href='http://localhost:5279/profile/orders' class='button'>View Order</a>
                        </p>
                        <p>We'll send you another email when your order ships.</p>
                    </div>
                    <div class='footer'>
                        <p>&copy; 2025 Sparkle. All rights reserved.</p>
                    </div>
                </div>
            </body>
            </html>
        ";

        await SendEmailAsync(to, subject, body);
    }

    public async Task SendOrderStatusUpdateAsync(string to, string orderNumber, string status)
    {
        var subject = $"Order Status Update - {orderNumber}";
        var body = $@"
            <html>
            <head>
                <style>
                    body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                    .header {{ background: #10b981; color: white; padding: 20px; text-align: center; }}
                    .content {{ background: #f9fafb; padding: 20px; }}
                    .status-box {{ background: white; padding: 15px; border-radius: 8px; margin: 20px 0; text-align: center; }}
                    .status {{ font-size: 24px; font-weight: bold; color: #10b981; }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <div class='header'>
                        <h1>Order Update</h1>
                    </div>
                    <div class='content'>
                        <p>Your order <strong>{orderNumber}</strong> has been updated.</p>
                        <div class='status-box'>
                            <div class='status'>{status}</div>
                        </div>
                        <p>Track your order anytime from your account dashboard.</p>
                    </div>
                </div>
            </body>
            </html>
        ";

        await SendEmailAsync(to, subject, body);
    }

    public async Task SendTicketResponseAsync(string to, string ticketNumber, string message)
    {
        var subject = $"Support Ticket Response - {ticketNumber}";
        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <h2>New Response to Your Support Ticket</h2>
                    <p><strong>Ticket:</strong> {ticketNumber}</p>
                    <div style='background: #f3f4f6; padding: 15px; border-radius: 8px; margin: 20px 0;'>
                        <p>{message}</p>
                    </div>
                    <p><a href='http://localhost:5279/profile/tickets'>View Ticket</a></p>
                </div>
            </body>
            </html>
        ";

        await SendEmailAsync(to, subject, body);
    }

    public async Task SendRefundApprovedAsync(string to, string orderNumber, decimal refundAmount)
    {
        var subject = $"Refund Approved - {orderNumber}";
        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <h2 style='color: #10b981;'>Refund Approved</h2>
                    <p>Your refund request for order <strong>{orderNumber}</strong> has been approved.</p>
                    <div style='background: #d1fae5; padding: 15px; border-radius: 8px; margin: 20px 0;'>
                        <p><strong>Refund Amount:</strong> ৳{refundAmount:N2}</p>
                        <p>The refund will be processed within 5-7 business days.</p>
                    </div>
                </div>
            </body>
            </html>
        ";

        await SendEmailAsync(to, subject, body);
    }
}
