using IBhayiPharmacyManagementSystem.Data;
using IBhayiPharmacyManagementSystem.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace IBhayiPharmacyManagementSystem.Services
{
    public interface ICustomerActivityService
    {
        Task LogActivityAsync(int customerId, string activityType, string description, string? entityType = null, int? entityId = null, string? additionalData = null);
    }

    public class CustomerActivityService : ICustomerActivityService
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CustomerActivityService(AppDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogActivityAsync(int customerId, string activityType, string description, string? entityType = null, int? entityId = null, string? additionalData = null)
        {
            try
            {
                var activity = new CustomerActivity
                {
                    CustomerId = customerId,
                    ActivityType = activityType,
                    Description = description,
                    EntityType = entityType,
                    EntityId = entityId,
                    Timestamp = DateTime.UtcNow,
                    IPAddress = GetClientIP(),
                    AdditionalData = additionalData,
                    IsRead = false
                };

                _context.CustomerActivities.Add(activity);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log error but don't throw - activity logging should not break the main functionality
                Console.WriteLine($"Error logging activity: {ex.Message}");
            }
        }

        private string? GetClientIP()
        {
            try
            {
                var request = _httpContextAccessor.HttpContext?.Request;
                if (request == null) return null;

                // Try to get real IP from various headers
                var ip = request.Headers["X-Forwarded-For"].FirstOrDefault() ??
                         request.Headers["X-Real-IP"].FirstOrDefault() ??
                         request.HttpContext.Connection.RemoteIpAddress?.ToString();

                return ip;
            }
            catch
            {
                return null;
            }
        }
    }
}

