using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IBhayiPharmacyManagementSystem.Services
{
    public interface IEmailService
    {
        Task SendPrescriptionReadyNotificationAsync(string customerEmail, string customerName, int prescriptionId, string medications, decimal totalAmount);
        Task SendPrescriptionReadyForReviewAsync(string customerEmail, string customerName, int prescriptionId, string medications);
        Task SendRepeatRequestNotificationAsync(string customerEmail, string customerName, string medicationName);
        Task SendRepeatReadyNotificationAsync(string customerEmail, string customerName, string medicationName, decimal amount);
        Task SendOrderReadyForCollectionNotificationAsync(string customerEmail, string customerName, int orderId, string orderDetails, decimal totalAmount);
        Task SendSupplierOrderNotificationAsync(string supplierEmail, string supplierName, int orderId, string orderDetails);
        Task SendOutOfStockNotificationToCustomerAsync(string customerEmail, string customerName, int orderId, string medicationName);
        Task SendOutOfStockNotificationToPharmacyManagerAsync(string managerEmail, string managerName, int orderId, string medicationName, int quantityOrdered);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendPrescriptionReadyForReviewAsync(string customerEmail, string customerName, int prescriptionId, string medications)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("IBhayi Pharmacy", _configuration["EmailSettings:FromEmail"] ?? "noreply@ibhayipharmacy.co.za"));
                message.To.Add(new MailboxAddress(customerName, customerEmail));
                message.Subject = $"GRP-04-08 - Your Prescription #{prescriptionId} is Ready";

                var bodyBuilder = new BodyBuilder();
                bodyBuilder.HtmlBody = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                        <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                            <div style='background-color: #2c5aa0; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0;'>
                                <h1 style='margin: 0; font-size: 24px;'>IBhayi Pharmacy</h1>
                                <p style='margin: 5px 0 0 0; font-size: 16px;'>Your Prescription is Ready</p>
                            </div>
                            
                            <div style='background-color: #f8f9fa; padding: 30px; border-radius: 0 0 8px 8px; border: 1px solid #dee2e6;'>
                                <h2 style='color: #2c5aa0; margin-top: 0;'>Hello {customerName},</h2>
                                
                                <p>Your prescription <strong>#{prescriptionId}</strong> has been processed and is ready for review.</p>
                                
                                <div style='background-color: white; padding: 20px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #28a745;'>
                                    <h3 style='color: #28a745; margin-top: 0;'>Prescription Details:</h3>
                                    <div style='white-space: pre-line;'>{medications}</div>
                                </div>
                                
                                <div style='background-color: #d1ecf1; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #17a2b8;'>
                                    <h4 style='color: #0c5460; margin-top: 0;'>Next Steps:</h4>
                                    <ul style='margin: 5px 0; color: #0c5460;'>
                                        <li>Please review your prescription details</li>
                                        <li>Contact us if you have any questions</li>
                                        <li>Visit us during pharmacy hours if you need assistance</li>
                                    </ul>
                                </div>
                                
                                <p>Thank you for choosing IBhayi Pharmacy!</p>
                                
                                <div style='margin-top: 30px; padding-top: 20px; border-top: 1px solid #dee2e6; text-align: center; color: #6c757d; font-size: 14px;'>
                                    <p>IBhayi Pharmacy Management System<br>
                                    This is an automated notification. Please do not reply to this email.</p>
                                </div>
                            </div>
                        </div>
                    </body>
                    </html>";

                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(_configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com", 
                    int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587"), 
                    MailKit.Security.SecureSocketOptions.StartTls);
                
                await client.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation($"Prescription ready (for review) notification sent to {customerEmail} for prescription #{prescriptionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send prescription ready (for review) notification to {customerEmail}");
            }
        }

        public async Task SendPrescriptionReadyNotificationAsync(string customerEmail, string customerName, int prescriptionId, string medications, decimal totalAmount)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("IBhayi Pharmacy", _configuration["EmailSettings:FromEmail"] ?? "noreply@ibhayipharmacy.co.za"));
                message.To.Add(new MailboxAddress(customerName, customerEmail));
                message.Subject = $"GRP-04-08 - Your Prescription #{prescriptionId} is Ready for Collection";

                var bodyBuilder = new BodyBuilder();
                bodyBuilder.HtmlBody = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                        <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                            <div style='background-color: #2c5aa0; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0;'>
                                <h1 style='margin: 0; font-size: 24px;'>IBhayi Pharmacy</h1>
                                <p style='margin: 5px 0 0 0; font-size: 16px;'>Your Prescription is Ready!</p>
                            </div>
                            
                            <div style='background-color: #f8f9fa; padding: 30px; border-radius: 0 0 8px 8px; border: 1px solid #dee2e6;'>
                                <h2 style='color: #2c5aa0; margin-top: 0;'>Hello {customerName},</h2>
                                
                                <p>Great news! Your prescription <strong>#{prescriptionId}</strong> has been processed and is ready for collection.</p>
                                
                                <div style='background-color: white; padding: 20px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #28a745;'>
                                    <h3 style='color: #28a745; margin-top: 0;'>Prescription Details:</h3>
                                    <div style='white-space: pre-line;'>{medications}</div>
                                </div>
                                
                                <div style='background-color: #fff3cd; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #ffc107;'>
                                    <h4 style='color: #856404; margin-top: 0;'>Amount Due: R{totalAmount:F2}</h4>
                                    <p style='margin: 5px 0 0 0; color: #856404;'>Please bring exact change or your preferred payment method when collecting.</p>
                                </div>
                                
                                <div style='background-color: #d1ecf1; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #17a2b8;'>
                                    <h4 style='color: #0c5460; margin-top: 0;'>Collection Instructions:</h4>
                                    <ul style='margin: 5px 0; color: #0c5460;'>
                                        <li>Please bring a valid ID</li>
                                        <li>Collection is available during pharmacy hours</li>
                                        <li>If you have any questions, please contact us</li>
                                    </ul>
                                </div>
                                
                                <p>Thank you for choosing IBhayi Pharmacy!</p>
                                
                                <div style='margin-top: 30px; padding-top: 20px; border-top: 1px solid #dee2e6; text-align: center; color: #6c757d; font-size: 14px;'>
                                    <p>IBhayi Pharmacy Management System<br>
                                    This is an automated notification. Please do not reply to this email.</p>
                                </div>
                            </div>
                        </div>
                    </body>
                    </html>";

                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(_configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com", 
                    int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587"), 
                    MailKit.Security.SecureSocketOptions.StartTls);
                
                await client.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation($"Prescription ready notification sent to {customerEmail} for prescription #{prescriptionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send prescription ready notification to {customerEmail}");
                // Don't throw - email failure shouldn't break the main flow
            }
        }

        public async Task SendRepeatRequestNotificationAsync(string customerEmail, string customerName, string medicationName)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("IBhayi Pharmacy", _configuration["EmailSettings:FromEmail"] ?? "noreply@ibhayipharmacy.co.za"));
                message.To.Add(new MailboxAddress(customerName, customerEmail));
                message.Subject = $"GRP-04-08 - Repeat Request Received - {medicationName}";

                var bodyBuilder = new BodyBuilder();
                bodyBuilder.HtmlBody = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                        <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                            <div style='background-color: #2c5aa0; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0;'>
                                <h1 style='margin: 0; font-size: 24px;'>IBhayi Pharmacy</h1>
                                <p style='margin: 5px 0 0 0; font-size: 16px;'>Repeat Request Received</p>
                            </div>
                            
                            <div style='background-color: #f8f9fa; padding: 30px; border-radius: 0 0 8px 8px; border: 1px solid #dee2e6;'>
                                <h2 style='color: #2c5aa0; margin-top: 0;'>Hello {customerName},</h2>
                                
                                <p>We have received your repeat request for <strong>{medicationName}</strong>.</p>
                                
                                <div style='background-color: #d4edda; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #28a745;'>
                                    <h4 style='color: #155724; margin-top: 0;'>What happens next?</h4>
                                    <ul style='margin: 5px 0; color: #155724;'>
                                        <li>Our pharmacist will review your request</li>
                                        <li>We will prepare your medication</li>
                                        <li>You will receive another email when it's ready for collection</li>
                                    </ul>
                                </div>
                                
                                <p>Thank you for choosing IBhayi Pharmacy!</p>
                                
                                <div style='margin-top: 30px; padding-top: 20px; border-top: 1px solid #dee2e6; text-align: center; color: #6c757d; font-size: 14px;'>
                                    <p>IBhayi Pharmacy Management System<br>
                                    This is an automated notification. Please do not reply to this email.</p>
                                </div>
                            </div>
                        </div>
                    </body>
                    </html>";

                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(_configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com", 
                    int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587"), 
                    MailKit.Security.SecureSocketOptions.StartTls);
                
                await client.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation($"Repeat request notification sent to {customerEmail} for {medicationName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send repeat request notification to {customerEmail}");
            }
        }

        public async Task SendRepeatReadyNotificationAsync(string customerEmail, string customerName, string medicationName, decimal amount)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("IBhayi Pharmacy", _configuration["EmailSettings:FromEmail"] ?? "noreply@ibhayipharmacy.co.za"));
                message.To.Add(new MailboxAddress(customerName, customerEmail));
                message.Subject = $"GRP-04-08 - Your Repeat for {medicationName} is Ready for Collection";

                var bodyBuilder = new BodyBuilder();
                bodyBuilder.HtmlBody = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                        <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                            <div style='background-color: #2c5aa0; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0;'>
                                <h1 style='margin: 0; font-size: 24px;'>IBhayi Pharmacy</h1>
                                <p style='margin: 5px 0 0 0; font-size: 16px;'>Your Repeat is Ready!</p>
                            </div>
                            
                            <div style='background-color: #f8f9fa; padding: 30px; border-radius: 0 0 8px 8px; border: 1px solid #dee2e6;'>
                                <h2 style='color: #2c5aa0; margin-top: 0;'>Hello {customerName},</h2>
                                
                                <p>Your repeat request for <strong>{medicationName}</strong> is ready for collection.</p>
                                
                                <div style='background-color: #fff3cd; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #ffc107;'>
                                    <h4 style='color: #856404; margin-top: 0;'>Amount Due: R{amount:F2}</h4>
                                    <p style='margin: 5px 0 0 0; color: #856404;'>Please bring exact change or your preferred payment method when collecting.</p>
                                </div>
                                
                                <div style='background-color: #d1ecf1; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #17a2b8;'>
                                    <h4 style='color: #0c5460; margin-top: 0;'>Collection Instructions:</h4>
                                    <ul style='margin: 5px 0; color: #0c5460;'>
                                        <li>Please bring a valid ID</li>
                                        <li>Collection is available during pharmacy hours</li>
                                        <li>If you have any questions, please contact us</li>
                                    </ul>
                                </div>
                                
                                <p>Thank you for choosing IBhayi Pharmacy!</p>
                                
                                <div style='margin-top: 30px; padding-top: 20px; border-top: 1px solid #dee2e6; text-align: center; color: #6c757d; font-size: 14px;'>
                                    <p>IBhayi Pharmacy Management System<br>
                                    This is an automated notification. Please do not reply to this email.</p>
                                </div>
                            </div>
                        </div>
                    </body>
                    </html>";

                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(_configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com", 
                    int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587"), 
                    MailKit.Security.SecureSocketOptions.StartTls);
                
                await client.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation($"Repeat ready notification sent to {customerEmail} for {medicationName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send repeat ready notification to {customerEmail}");
            }
        }

        public async Task SendOrderReadyForCollectionNotificationAsync(string customerEmail, string customerName, int orderId, string orderDetails, decimal totalAmount)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("IBhayi Pharmacy", _configuration["EmailSettings:FromEmail"] ?? "noreply@ibhayipharmacy.co.za"));
                message.To.Add(new MailboxAddress(customerName, customerEmail));
                message.Subject = $"GRP-04-08 - Order #{orderId} Ready For Collection";

                var bodyBuilder = new BodyBuilder();
                bodyBuilder.HtmlBody = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                        <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                            <div style='background-color: #2c5aa0; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0;'>
                                <h1 style='margin: 0; font-size: 24px;'>IBhayi Pharmacy</h1>
                                <p style='margin: 5px 0 0 0; font-size: 16px;'>Your Order is Ready!</p>
                            </div>
                            
                            <div style='background-color: #f8f9fa; padding: 30px; border-radius: 0 0 8px 8px; border: 1px solid #dee2e6;'>
                                <h2 style='color: #2c5aa0; margin-top: 0;'>Hello {customerName},</h2>
                                
                                <p>Great news! Your order <strong>#{orderId}</strong> has been processed and is ready for collection.</p>
                                
                                <div style='background-color: white; padding: 20px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #28a745;'>
                                    <h3 style='color: #28a745; margin-top: 0;'>Order Details:</h3>
                                    <div style='white-space: pre-line;'>{orderDetails}</div>
                                </div>
                                
                                <div style='background-color: #fff3cd; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #ffc107;'>
                                    <h4 style='color: #856404; margin-top: 0;'>Total Amount Due: R{totalAmount:F2}</h4>
                                    <p style='margin: 5px 0 0 0; color: #856404;'>Please bring exact change or your preferred payment method when collecting.</p>
                                </div>
                                
                                <div style='background-color: #d1ecf1; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #17a2b8;'>
                                    <h4 style='color: #0c5460; margin-top: 0;'>Collection Instructions:</h4>
                                    <ul style='margin: 5px 0; color: #0c5460;'>
                                        <li>Please bring a valid ID</li>
                                        <li>Collection is available during pharmacy hours</li>
                                        <li>If you have any questions, please contact us</li>
                                    </ul>
                                </div>
                                
                                <p>Thank you for choosing IBhayi Pharmacy!</p>
                                
                                <div style='margin-top: 30px; padding-top: 20px; border-top: 1px solid #dee2e6; text-align: center; color: #6c757d; font-size: 14px;'>
                                    <p>IBhayi Pharmacy Management System<br>
                                    This is an automated notification. Please do not reply to this email.</p>
                                </div>
                            </div>
                        </div>
                    </body>
                    </html>";

                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(_configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com", 
                    int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587"), 
                    MailKit.Security.SecureSocketOptions.StartTls);
                
                await client.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation($"Order ready for collection notification sent to {customerEmail} for order #{orderId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send order ready for collection notification to {customerEmail}");
            }
        }

        public async Task SendSupplierOrderNotificationAsync(string supplierEmail, string supplierName, int orderId, string orderDetails)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("IBhayi Pharmacy", _configuration["EmailSettings:FromEmail"] ?? "noreply@ibhayipharmacy.co.za"));
                message.To.Add(new MailboxAddress(supplierName, supplierEmail));
                message.Subject = $"GRP-04-08 - New Order Received - Order ID: {orderId}";

                var bodyBuilder = new BodyBuilder();
                bodyBuilder.HtmlBody = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                        <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                            <div style='background-color: #2c5aa0; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0;'>
                                <h1 style='margin: 0; font-size: 24px;'>IBhayi Pharmacy</h1>
                                <p style='margin: 5px 0 0 0; font-size: 16px;'>New Order for Your Products</p>
                            </div>
                            
                            <div style='background-color: #f8f9fa; padding: 30px; border-radius: 0 0 8px 8px; border: 1px solid #dee2e6;'>
                                <h2 style='color: #2c5aa0; margin-top: 0;'>Hello {supplierName},</h2>
                                
                                <p>A new order containing your products has been placed through the IBhayi Pharmacy Management System.</p>
                                
                                <div style='background-color: white; padding: 20px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #007bff;'>
                                    <h3 style='color: #007bff; margin-top: 0;'>Order Details:</h3>
                                    <div style='white-space: pre-line;'>{orderDetails}</div>
                                </div>
                                
                                <p>Please prepare these items for dispatch.</p>
                                <p>Thank you for your continued partnership with IBhayi Pharmacy!</p>
                                
                                <div style='margin-top: 30px; padding-top: 20px; border-top: 1px solid #dee2e6; text-align: center; color: #6c757d; font-size: 14px;'>
                                    <p>IBhayi Pharmacy Management System<br>
                                    This is an automated notification. Please do not reply to this email.</p>
                                </div>
                            </div>
                        </div>
                    </body>
                    </html>";

                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(_configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com", 
                    int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587"), 
                    MailKit.Security.SecureSocketOptions.StartTls);
                
                await client.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation($"Supplier order notification sent to {supplierEmail} for order #{orderId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send supplier order notification to {supplierEmail}");
            }
        }

        public async Task SendOutOfStockNotificationToCustomerAsync(string customerEmail, string customerName, int orderId, string medicationName)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("IBhayi Pharmacy", _configuration["EmailSettings:FromEmail"] ?? "noreply@ibhayipharmacy.co.za"));
                message.To.Add(new MailboxAddress(customerName, customerEmail));
                message.Subject = $"GRP-04-08 - Order #{orderId} - Medication Out of Stock";

                var bodyBuilder = new BodyBuilder();
                bodyBuilder.HtmlBody = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                        <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                            <div style='background-color: #dc3545; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0;'>
                                <h1 style='margin: 0; font-size: 24px;'>IBhayi Pharmacy</h1>
                                <p style='margin: 5px 0 0 0; font-size: 16px;'>Medication Out of Stock</p>
                            </div>
                            
                            <div style='background-color: #f8f9fa; padding: 30px; border-radius: 0 0 8px 8px; border: 1px solid #dee2e6;'>
                                <h2 style='color: #dc3545; margin-top: 0;'>Hello {customerName},</h2>
                                
                                <p>We regret to inform you that <strong>{medicationName}</strong> from your order <strong>#{orderId}</strong> is currently out of stock.</p>
                                
                                <div style='background-color: #f8d7da; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #dc3545;'>
                                    <h4 style='color: #721c24; margin-top: 0;'>What this means:</h4>
                                    <ul style='margin: 5px 0; color: #721c24;'>
                                        <li>We are working to restock this medication as soon as possible</li>
                                        <li>Your order will be processed once the medication is available</li>
                                        <li>You will receive another notification when it's ready</li>
                                    </ul>
                                </div>
                                
                                <div style='background-color: #d1ecf1; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #17a2b8;'>
                                    <h4 style='color: #0c5460; margin-top: 0;'>What you can do:</h4>
                                    <ul style='margin: 5px 0; color: #0c5460;'>
                                        <li>Please be patient while we restock</li>
                                        <li>Contact us if you have any questions</li>
                                        <li>We apologize for any inconvenience</li>
                                    </ul>
                                </div>
                                
                                <p>We appreciate your understanding and patience. Thank you for choosing IBhayi Pharmacy!</p>
                                
                                <div style='margin-top: 30px; padding-top: 20px; border-top: 1px solid #dee2e6; text-align: center; color: #6c757d; font-size: 14px;'>
                                    <p>IBhayi Pharmacy Management System<br>
                                    This is an automated notification. Please do not reply to this email.</p>
                                </div>
                            </div>
                        </div>
                    </body>
                    </html>";

                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(_configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com", 
                    int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587"), 
                    MailKit.Security.SecureSocketOptions.StartTls);
                
                await client.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation($"Out of stock notification sent to customer {customerEmail} for order #{orderId}, medication: {medicationName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send out of stock notification to customer {customerEmail}");
            }
        }

        public async Task SendOutOfStockNotificationToPharmacyManagerAsync(string managerEmail, string managerName, int orderId, string medicationName, int quantityOrdered)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("IBhayi Pharmacy", _configuration["EmailSettings:FromEmail"] ?? "noreply@ibhayipharmacy.co.za"));
                message.To.Add(new MailboxAddress(managerName, managerEmail));
                message.Subject = $"GRP-04-08 - URGENT: Medication Out of Stock - Order #{orderId}";

                var bodyBuilder = new BodyBuilder();
                bodyBuilder.HtmlBody = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                        <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                            <div style='background-color: #dc3545; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0;'>
                                <h1 style='margin: 0; font-size: 24px;'>IBhayi Pharmacy</h1>
                                <p style='margin: 5px 0 0 0; font-size: 16px;'>URGENT: Stock Alert</p>
                            </div>
                            
                            <div style='background-color: #f8f9fa; padding: 30px; border-radius: 0 0 8px 8px; border: 1px solid #dee2e6;'>
                                <h2 style='color: #dc3545; margin-top: 0;'>Hello {managerName},</h2>
                                
                                <p><strong>URGENT STOCK ALERT:</strong> A customer order cannot be fulfilled due to insufficient stock.</p>
                                
                                <div style='background-color: #f8d7da; padding: 20px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #dc3545;'>
                                    <h3 style='color: #721c24; margin-top: 0;'>Order Details:</h3>
                                    <ul style='margin: 5px 0; color: #721c24;'>
                                        <li><strong>Order ID:</strong> #{orderId}</li>
                                        <li><strong>Medication:</strong> {medicationName}</li>
                                        <li><strong>Quantity Ordered:</strong> {quantityOrdered}</li>
                                        <li><strong>Status:</strong> Out of Stock</li>
                                    </ul>
                                </div>
                                
                                <div style='background-color: #fff3cd; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #ffc107;'>
                                    <h4 style='color: #856404; margin-top: 0;'>Required Actions:</h4>
                                    <ul style='margin: 5px 0; color: #856404;'>
                                        <li>Check current stock levels for {medicationName}</li>
                                        <li>Place urgent stock order if needed</li>
                                        <li>Update customer on expected availability</li>
                                        <li>Consider alternative medications if appropriate</li>
                                    </ul>
                                </div>
                                
                                <p>Please take immediate action to resolve this stock issue. Customer satisfaction depends on quick resolution.</p>
                                
                                <div style='margin-top: 30px; padding-top: 20px; border-top: 1px solid #dee2e6; text-align: center; color: #6c757d; font-size: 14px;'>
                                    <p>IBhayi Pharmacy Management System<br>
                                    This is an automated notification. Please do not reply to this email.</p>
                                </div>
                            </div>
                        </div>
                    </body>
                    </html>";

                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(_configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com", 
                    int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587"), 
                    MailKit.Security.SecureSocketOptions.StartTls);
                
                await client.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation($"Out of stock alert sent to pharmacy manager {managerEmail} for order #{orderId}, medication: {medicationName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send out of stock alert to pharmacy manager {managerEmail}");
            }
        }
    }
}
