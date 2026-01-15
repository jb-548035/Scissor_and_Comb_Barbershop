using SQLite;

namespace hair.Models
{
    [Table("WalkInTransactions")]
    public class WalkInTransaction
    {
        [PrimaryKey, AutoIncrement]
        public int WalkInId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int ServiceId { get; set; }
        public int BarberId { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime TransactionDate { get; set; } = DateTime.Now;
        public DateTime ServiceDate { get; set; } = DateTime.Now;
        public string Status { get; set; } = "Paid"; // 'Paid', 'Pending', 'Cancelled'
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public int? CreatedByUserId { get; set; }
    }
}
