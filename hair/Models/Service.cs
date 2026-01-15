using SQLite;

namespace hair.Models
{
    [Table("Services")]
    public class Service
    {
        [PrimaryKey, AutoIncrement]
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
