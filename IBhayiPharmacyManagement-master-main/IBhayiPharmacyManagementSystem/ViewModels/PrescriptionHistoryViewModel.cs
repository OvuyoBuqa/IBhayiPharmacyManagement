using IBhayiPharmacyManagementSystem.Models;

namespace IBhayiPharmacyManagementSystem.ViewModels
{
    public class PrescriptionHistoryViewModel
    {
        public List<UnprocessedScript> Prescriptions { get; set; } = new List<UnprocessedScript>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
        public int PageSize { get; set; }
        public string? SearchTerm { get; set; }

        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
        public int StartPage => Math.Max(1, CurrentPage - 2);
        public int EndPage => Math.Min(TotalPages, CurrentPage + 2);
    }
}
