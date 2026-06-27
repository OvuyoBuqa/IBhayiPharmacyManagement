using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IBhayiPharmacyManagementSystem.Services
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string message)
        {
            try
            {
                var smtpSettings = _configuration.GetSection("SmtpSettings");
                var mailMessage = new MailMessage
                {
                    // Use the authenticated username as the From address to satisfy SMTP servers (e.g., Gmail)
                    From = new MailAddress(smtpSettings["Username"], "IBhayi Pharmacy"),
                    Subject = subject,
                    Body = message,
                    // Registration emails are plain text; set to false to avoid formatting issues
                    IsBodyHtml = false,
                };
                mailMessage.To.Add(toEmail);

                using (var client = new SmtpClient(smtpSettings["Server"])) 
                {
                    client.Port = int.Parse(smtpSettings["Port"]);
                    client.Credentials = new System.Net.NetworkCredential(smtpSettings["Username"], smtpSettings["Password"]);
                    client.EnableSsl = bool.Parse(smtpSettings["EnableSsl"]);
                    await client.SendMailAsync(mailMessage);
                    _logger.LogInformation($"Email sent successfully to {toEmail} with subject: {subject}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email to {ToEmail} with subject {Subject}", toEmail, subject);
                // Do not throw to avoid breaking higher-level flows (e.g., staff registration)
            }
        }
    }
}
