using SQLite;

namespace hair.Models
{
    [Table("Barbers")]
    public class Barber
    {
        public int BarberId { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }

        // 🔹 Derived fields (NOT database columns)
        public string AvailabilityDays { get; set; } = "";       // "1,2,4,6"
        public string AvailabilityTimeStart { get; set; } = "";  // "09:00"
        public string AvailabilityTimeEnd { get; set; } = "";    // "19:00"
        public decimal CommissionRate { get; set; } = 0.10m;      // 10% default commission

        public string FullName => $"{FirstName} {LastName}";
        public string AvailabilityTimeFormatted
        {
            get
            {
                if (DateTime.TryParse(AvailabilityTimeStart, out var start) &&
                    DateTime.TryParse(AvailabilityTimeEnd, out var end))
                {
                    return $"{start:hh:mmtt}-{end:hh:mmtt}"; // e.g., 10:00AM-05:00PM
                }
                return $"{AvailabilityTimeStart}-{AvailabilityTimeEnd}";
            }
        }

    }

}
