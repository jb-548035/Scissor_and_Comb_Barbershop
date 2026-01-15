using SQLite;

namespace hair.Models
{
    [Table("Bookings")]
    public class Booking
    {
        [PrimaryKey, AutoIncrement]
        public int BookingId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int ServiceId { get; set; }
        public int BarberId { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal DepositAmount { get; set; }
        public DateTime BookingDate { get; set; } = DateTime.Now;
        public DateTime Schedule { get; set; }
        public string Status { get; set; } = "Pending"; // 'Pending', 'Confirmed', 'Cancelled'
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public int? CreatedByUserId { get; set; }
    }
}
