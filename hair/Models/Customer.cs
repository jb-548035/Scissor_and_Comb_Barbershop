using SQLite;

namespace hair.Models
{
    [Table("Customers")]
    public class Customer
    {
        [PrimaryKey, AutoIncrement]
        public int CustomerId { get; set; }
        
        public string FirstName { get; set; } = string.Empty;
        
        public string LastName { get; set; } = string.Empty;
        
        public string Phone { get; set; } = string.Empty;
        
        public string Email { get; set; } = string.Empty;
        
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        
        [Ignore]
        public string FullName => $"{FirstName} {LastName}";
    }
}
