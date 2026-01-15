namespace hair.Services
{
    public static class SharedData
    {

        // All bookings & transactions
        public static List<TransactionRecord> Records { get; } = new List<TransactionRecord>();
        public static List<TransactionRecord> WalkInRecords { get; } = new List<TransactionRecord>();

        // Service base prices
        public static readonly Dictionary<string, double> ServicePrices = new()
        {
            { "Haircut", 120 },
            { "Haircut + Dye", 150 }
        };

        // Add-on prices
        public static readonly Dictionary<string, double> AddOnPrices = new()
        {
            { "None", 0 }
        };

        // Helper: compute total price from service + addon (returns base + addon)
        public static double ComputeTotal(string service, string addon)
        {
            double s = 0, a = 0;
            if (!string.IsNullOrWhiteSpace(service) && ServicePrices.TryGetValue(service, out var sv)) s = sv;
            if (!string.IsNullOrWhiteSpace(addon) && AddOnPrices.TryGetValue(addon, out var av)) a = av;
            return s + a;
        }

        // Simple helper to avoid duplicates by reference; identify by timestamp+name+service
        public static void AddRecord(TransactionRecord r)
        {
            if (r == null) return;
            // basic duplicate detection
            var exists = Records.Any(x =>
                x.CustomerName == r.CustomerName &&
                x.ServiceType == r.ServiceType &&
                x.DateTime == r.DateTime &&
                Math.Abs(x.TotalPrice - r.TotalPrice) < 0.01);

            if (!exists) Records.Add(r);
        }

        // Remove record
        public static void RemoveRecord(TransactionRecord r)
        {
            if (r == null) return;
            if (Records.Contains(r)) Records.Remove(r);
        }
    }

    public class TransactionRecord
    {
        public int WalkInId { get; set; } = 0;
        public int BookingId { get; set; } = 0;
        public string CustomerName { get; set; } = string.Empty;
        public string ServiceType { get; set; } = string.Empty;
        public string BarberName { get; set; } = string.Empty;
        public string AddOns { get; set; } = string.Empty;
        public double TotalPrice { get; set; }
        public double? Deposit { get; set; }
        public string DateTime { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = "Pending";
    }

}
