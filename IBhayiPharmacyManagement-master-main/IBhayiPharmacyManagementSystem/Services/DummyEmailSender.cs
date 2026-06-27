using System.Threading.Tasks;
using System.Diagnostics;

namespace IBhayiPharmacyManagementSystem.Services
{
    public class DummyEmailSender : IEmailSender
    {
        public Task SendEmailAsync(string toEmail, string subject, string message)
        {
            Debug.WriteLine($"Sending email to: {toEmail}");
            Debug.WriteLine($"Subject: {subject}");
            Debug.WriteLine($"Message: {message}");
            // In a real application, you would integrate with an actual email sending service here
            return Task.CompletedTask;
        }
    }
}
