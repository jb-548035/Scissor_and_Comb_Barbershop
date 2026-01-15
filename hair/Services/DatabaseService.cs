using hair.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Maui.Networking;
using System.Diagnostics;

namespace hair.Services
{
    /// <summary>
    /// Basic database service implementation
    /// </summary>
    public class DatabaseService
    {
        private readonly string _serverName;
        private readonly string _databaseName;
        private readonly string? _userId;
        private readonly string? _password;
        private readonly string _connectionString;

        private static bool IsInvalidColumnName(SqlException ex, string columnName)
        {
            return ex.Message.IndexOf($"Invalid column name '{columnName}'", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public DatabaseService(string serverName, string databaseName, string? userId = null, string? password = null)
        {
            _serverName = serverName;
            _databaseName = databaseName;
            _userId = userId;
            _password = password;

            if (string.IsNullOrWhiteSpace(_userId))
            {
                _connectionString = $"Server={_serverName};Database={_databaseName};Integrated Security=true;Encrypt=True;TrustServerCertificate=true;";
            }
            else
            {
                _connectionString = $"Server={_serverName};Database={_databaseName};User Id={_userId};Password={_password};Encrypt=True;TrustServerCertificate=true;";
            }
            Debug.WriteLine($"[DatabaseService] Initialized for {serverName}/{databaseName}");
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand("SELECT 1", connection))
                    {
                        await command.ExecuteScalarAsync();
                    }
                }

                Debug.WriteLine("[DatabaseService] Connection test successful");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] Connection test failed: {ex.Message}");
                return false;
            }
        }

        #region Audit Log Operations
        public async Task<List<AuditLog>> GetAuditLogsAsync()
        {
            try
            {
                var logs = new List<AuditLog>();

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"SELECT AuditLogId, TableName, RecordId, Action, OldValues, NewValues, ChangedByUserId, ChangedDate
                                  FROM AuditLogs
                                  ORDER BY ChangedDate DESC";

                    using (var command = new SqlCommand(query, connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            logs.Add(new AuditLog
                            {
                                AuditLogId = reader.GetInt32(0),
                                TableName = reader.GetString(1),
                                RecordId = reader.GetInt32(2),
                                Action = reader.GetString(3),
                                OldValues = reader.IsDBNull(4) ? null : reader.GetString(4),
                                NewValues = reader.IsDBNull(5) ? null : reader.GetString(5),
                                ChangedByUserId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                                ChangedDate = reader.GetDateTime(7)
                            });
                        }
                    }
                }

                Debug.WriteLine("[DatabaseService] Getting audit logs");
                return logs;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] GetAuditLogsAsync failed: {ex.Message}");
                return new List<AuditLog>();
            }
        }

        public async Task<bool> LogAuditAsync(string tableName, int recordId, string action, string oldValues, string newValues, int? changedByUserId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"INSERT INTO AuditLogs (TableName, RecordId, Action, OldValues, NewValues, ChangedByUserId)
                                  VALUES (@TableName, @RecordId, @Action, @OldValues, @NewValues, @ChangedByUserId)";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@TableName", tableName);
                        command.Parameters.AddWithValue("@RecordId", recordId);
                        command.Parameters.AddWithValue("@Action", action);
                        command.Parameters.AddWithValue("@OldValues", string.IsNullOrEmpty(oldValues) ? (object)DBNull.Value : oldValues);
                        command.Parameters.AddWithValue("@NewValues", string.IsNullOrEmpty(newValues) ? (object)DBNull.Value : newValues);
                        command.Parameters.AddWithValue("@ChangedByUserId", (object?)changedByUserId ?? DBNull.Value);
                        await command.ExecuteNonQueryAsync();
                    }
                }

                Debug.WriteLine($"[DatabaseService] Logging audit: {action} on {tableName}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] LogAuditAsync failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteAuditLogAsync(int auditLogId, int? changedByUserId = null)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand("DELETE FROM AuditLogs WHERE AuditLogId = @AuditLogId", connection))
                    {
                        command.Parameters.AddWithValue("@AuditLogId", auditLogId);
                        var rows = await command.ExecuteNonQueryAsync();
                        Debug.WriteLine($"[DatabaseService] Deleting audit log: {auditLogId}");
                        return rows > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] DeleteAuditLogAsync failed: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region Service Operations
        public async Task<List<Service>> GetServicesAsync()
        {
            try
            {
                var services = new List<Service>();

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand("SELECT ServiceId, ServiceName, Price, CreatedDate FROM Services ORDER BY ServiceName", connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            services.Add(new Service
                            {
                                ServiceId = reader.GetInt32(0),
                                ServiceName = reader.GetString(1),
                                Price = reader.GetDecimal(2),
                                CreatedDate = reader.GetDateTime(3)
                            });
                        }
                    }
                }

                Debug.WriteLine("[DatabaseService] Getting services");
                return services;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] GetServicesAsync failed: {ex.Message}");
                return new List<Service>();
            }
        }

        public async Task<bool> CreateServiceAsync(string serviceName, decimal price, string description, int? createdByUserId)
        {
            try
            {
                var id = await CreateServiceAsync(serviceName, price, createdByUserId);
                Debug.WriteLine($"[DatabaseService] Creating service: {serviceName}");
                return id > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] CreateServiceAsync failed: {ex.Message}");
                return false;
            }
        }

        public async Task<int> CreateServiceAsync(string serviceName, decimal price, int? createdByUserId = null)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"INSERT INTO Services (ServiceName, Price)
                                  VALUES (@ServiceName, @Price);
                                  SELECT CAST(SCOPE_IDENTITY() AS INT);";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ServiceName", serviceName);
                        command.Parameters.AddWithValue("@Price", price);
                        var result = await command.ExecuteScalarAsync();
                        return result == null ? 0 : Convert.ToInt32(result);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] CreateServiceAsync failed: {ex.Message}");
                return 0;
            }
        }

        public async Task<bool> UpdateServiceAsync(int serviceId, string serviceName, decimal price, int? changedByUserId = null)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(@"UPDATE Services
                                                        SET ServiceName = @ServiceName,
                                                            Price = @Price
                                                        WHERE ServiceId = @ServiceId", connection))
                    {
                        command.Parameters.AddWithValue("@ServiceId", serviceId);
                        command.Parameters.AddWithValue("@ServiceName", serviceName);
                        command.Parameters.AddWithValue("@Price", price);
                        var rows = await command.ExecuteNonQueryAsync();
                        Debug.WriteLine($"[DatabaseService] Updating service: {serviceId}");
                        return rows > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] UpdateServiceAsync failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteServiceAsync(int serviceId, int? changedByUserId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand("DELETE FROM Services WHERE ServiceId = @ServiceId", connection))
                    {
                        command.Parameters.AddWithValue("@ServiceId", serviceId);
                        var rows = await command.ExecuteNonQueryAsync();
                        Debug.WriteLine($"[DatabaseService] Deleting service: {serviceId}");
                        return rows > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] DeleteServiceAsync failed: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region Barber Operations
        public async Task<List<Barber>> GetBarbersAsync()
        {
            try
            {
                var barbers = new List<Barber>();

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    try
                    {
                        var queryWithAvailability = @"SELECT BarberId, FirstName, LastName, Email, Phone, CommissionRate, IsActive, CreatedDate,
                                                            AvailabilityDays, AvailabilityTimeStart, AvailabilityTimeEnd
                                                     FROM Barbers
                                                     ORDER BY FirstName, LastName";

                        using (var command = new SqlCommand(queryWithAvailability, connection))
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                barbers.Add(new Barber
                                {
                                    BarberId = reader.GetInt32(0),
                                    FirstName = reader.GetString(1),
                                    LastName = reader.GetString(2),
                                    Email = reader.GetString(3),
                                    Phone = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    CommissionRate = reader.IsDBNull(5) ? 0m : reader.GetDecimal(5),
                                    IsActive = reader.GetBoolean(6),
                                    CreatedDate = reader.GetDateTime(7),
                                    AvailabilityDays = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                    AvailabilityTimeStart = reader.IsDBNull(9) ? "" : reader.GetString(9),
                                    AvailabilityTimeEnd = reader.IsDBNull(10) ? "" : reader.GetString(10)
                                });
                            }
                        }
                    }
                    catch (SqlException ex) when (
                        IsInvalidColumnName(ex, "AvailabilityDays") ||
                        IsInvalidColumnName(ex, "AvailabilityTimeStart") ||
                        IsInvalidColumnName(ex, "AvailabilityTimeEnd"))
                    {
                        var query = @"SELECT BarberId, FirstName, LastName, Email, Phone, CommissionRate, IsActive, CreatedDate
                                      FROM Barbers
                                      ORDER BY FirstName, LastName";

                        using (var command = new SqlCommand(query, connection))
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                barbers.Add(new Barber
                                {
                                    BarberId = reader.GetInt32(0),
                                    FirstName = reader.GetString(1),
                                    LastName = reader.GetString(2),
                                    Email = reader.GetString(3),
                                    Phone = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    CommissionRate = reader.IsDBNull(5) ? 0m : reader.GetDecimal(5),
                                    IsActive = reader.GetBoolean(6),
                                    CreatedDate = reader.GetDateTime(7),
                                    AvailabilityDays = "",
                                    AvailabilityTimeStart = "",
                                    AvailabilityTimeEnd = ""
                                });
                            }
                        }
                    }
                }

                Debug.WriteLine("[DatabaseService] Getting barbers");
                return barbers;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] GetBarbersAsync failed: {ex.Message}");
                return new List<Barber>();
            }
        }

        public async Task<Barber?> GetBarberByIdAsync(int barberId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    try
                    {
                        var queryWithAvailability = @"SELECT BarberId, FirstName, LastName, Email, Phone, CommissionRate, IsActive, CreatedDate,
                                                            AvailabilityDays, AvailabilityTimeStart, AvailabilityTimeEnd
                                                     FROM Barbers
                                                     WHERE BarberId = @BarberId";

                        using (var command = new SqlCommand(queryWithAvailability, connection))
                        {
                            command.Parameters.AddWithValue("@BarberId", barberId);

                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                if (!await reader.ReadAsync())
                                    return null;

                                Debug.WriteLine($"[DatabaseService] Getting barber by ID: {barberId}");
                                return new Barber
                                {
                                    BarberId = reader.GetInt32(0),
                                    FirstName = reader.GetString(1),
                                    LastName = reader.GetString(2),
                                    Email = reader.GetString(3),
                                    Phone = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    CommissionRate = reader.IsDBNull(5) ? 0m : reader.GetDecimal(5),
                                    IsActive = reader.GetBoolean(6),
                                    CreatedDate = reader.GetDateTime(7),
                                    AvailabilityDays = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                    AvailabilityTimeStart = reader.IsDBNull(9) ? "" : reader.GetString(9),
                                    AvailabilityTimeEnd = reader.IsDBNull(10) ? "" : reader.GetString(10)
                                };
                            }
                        }
                    }
                    catch (SqlException ex) when (
                        IsInvalidColumnName(ex, "AvailabilityDays") ||
                        IsInvalidColumnName(ex, "AvailabilityTimeStart") ||
                        IsInvalidColumnName(ex, "AvailabilityTimeEnd"))
                    {
                        var query = @"SELECT BarberId, FirstName, LastName, Email, Phone, CommissionRate, IsActive, CreatedDate
                                      FROM Barbers
                                      WHERE BarberId = @BarberId";

                        using (var command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@BarberId", barberId);

                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                if (!await reader.ReadAsync())
                                    return null;

                                Debug.WriteLine($"[DatabaseService] Getting barber by ID: {barberId}");
                                return new Barber
                                {
                                    BarberId = reader.GetInt32(0),
                                    FirstName = reader.GetString(1),
                                    LastName = reader.GetString(2),
                                    Email = reader.GetString(3),
                                    Phone = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    CommissionRate = reader.IsDBNull(5) ? 0m : reader.GetDecimal(5),
                                    IsActive = reader.GetBoolean(6),
                                    CreatedDate = reader.GetDateTime(7),
                                    AvailabilityDays = "",
                                    AvailabilityTimeStart = "",
                                    AvailabilityTimeEnd = ""
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] GetBarberByIdAsync failed: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> CreateBarberAsync(string firstName, string lastName, string email, string phone, string availabilityDays, string availabilityTimeStart, string availabilityTimeEnd)
        {
            try
            {
                var barberId = await CreateBarberAsync(firstName, lastName, email, phone, 0m, availabilityDays, availabilityTimeStart, availabilityTimeEnd);
                Debug.WriteLine($"[DatabaseService] Creating barber: {firstName} {lastName}");
                return barberId > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] CreateBarberAsync failed: {ex.Message}");
                return false;
            }
        }

        public async Task<int> CreateBarberAsync(string firstName, string lastName, string email, string phone, decimal commissionRate, string availabilityDays, string availabilityTimeStart, string availabilityTimeEnd)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    try
                    {
                        var queryWithAvailability = @"INSERT INTO Barbers (FirstName, LastName, Email, Phone, CommissionRate, IsActive, AvailabilityDays, AvailabilityTimeStart, AvailabilityTimeEnd)
                                                    VALUES (@FirstName, @LastName, @Email, @Phone, @CommissionRate, 1, @AvailabilityDays, @AvailabilityTimeStart, @AvailabilityTimeEnd);
                                                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        using (var command = new SqlCommand(queryWithAvailability, connection))
                        {
                            command.Parameters.AddWithValue("@FirstName", firstName);
                            command.Parameters.AddWithValue("@LastName", lastName);
                            command.Parameters.AddWithValue("@Email", email);
                            command.Parameters.AddWithValue("@Phone", string.IsNullOrEmpty(phone) ? (object)DBNull.Value : phone);
                            command.Parameters.AddWithValue("@CommissionRate", commissionRate);
                            command.Parameters.AddWithValue("@AvailabilityDays", availabilityDays ?? "");
                            command.Parameters.AddWithValue("@AvailabilityTimeStart", availabilityTimeStart ?? "");
                            command.Parameters.AddWithValue("@AvailabilityTimeEnd", availabilityTimeEnd ?? "");
                            var result = await command.ExecuteScalarAsync();
                            Debug.WriteLine($"[DatabaseService] Creating barber: {firstName} {lastName}");
                            return result == null ? 0 : Convert.ToInt32(result);
                        }
                    }
                    catch (SqlException ex) when (
                        IsInvalidColumnName(ex, "AvailabilityDays") ||
                        IsInvalidColumnName(ex, "AvailabilityTimeStart") ||
                        IsInvalidColumnName(ex, "AvailabilityTimeEnd"))
                    {
                        var query = @"INSERT INTO Barbers (FirstName, LastName, Email, Phone, CommissionRate, IsActive)
                                      VALUES (@FirstName, @LastName, @Email, @Phone, @CommissionRate, 1);
                                      SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        using (var command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@FirstName", firstName);
                            command.Parameters.AddWithValue("@LastName", lastName);
                            command.Parameters.AddWithValue("@Email", email);
                            command.Parameters.AddWithValue("@Phone", string.IsNullOrEmpty(phone) ? (object)DBNull.Value : phone);
                            command.Parameters.AddWithValue("@CommissionRate", commissionRate);
                            var result = await command.ExecuteScalarAsync();
                            Debug.WriteLine($"[DatabaseService] Creating barber: {firstName} {lastName}");
                            return result == null ? 0 : Convert.ToInt32(result);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] CreateBarberAsync failed: {ex.Message}");
                return 0;
            }
        }

        public async Task<bool> UpdateBarberAvailabilityAsync(int barberId, bool isActive, string availabilityDays, string availabilityTimeStart, string availabilityTimeEnd, int? changedByUserId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    try
                    {
                        using (var command = new SqlCommand(@"UPDATE Barbers
                                                            SET IsActive = @IsActive,
                                                                AvailabilityDays = @AvailabilityDays,
                                                                AvailabilityTimeStart = @AvailabilityTimeStart,
                                                                AvailabilityTimeEnd = @AvailabilityTimeEnd
                                                            WHERE BarberId = @BarberId", connection))
                        {
                            command.Parameters.AddWithValue("@BarberId", barberId);
                            command.Parameters.AddWithValue("@IsActive", isActive);
                            command.Parameters.AddWithValue("@AvailabilityDays", availabilityDays ?? "");
                            command.Parameters.AddWithValue("@AvailabilityTimeStart", availabilityTimeStart ?? "");
                            command.Parameters.AddWithValue("@AvailabilityTimeEnd", availabilityTimeEnd ?? "");
                            var rows = await command.ExecuteNonQueryAsync();
                            Debug.WriteLine($"[DatabaseService] Updating barber availability: {barberId}");
                            return rows > 0;
                        }
                    }
                    catch (SqlException ex) when (
                        IsInvalidColumnName(ex, "AvailabilityDays") ||
                        IsInvalidColumnName(ex, "AvailabilityTimeStart") ||
                        IsInvalidColumnName(ex, "AvailabilityTimeEnd"))
                    {
                        using (var command = new SqlCommand(@"UPDATE Barbers
                                                            SET IsActive = @IsActive
                                                            WHERE BarberId = @BarberId", connection))
                        {
                            command.Parameters.AddWithValue("@BarberId", barberId);
                            command.Parameters.AddWithValue("@IsActive", isActive);
                            var rows = await command.ExecuteNonQueryAsync();
                            Debug.WriteLine($"[DatabaseService] Updating barber availability: {barberId}");
                            return rows > 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] UpdateBarberAvailabilityAsync failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateBarberStatusAsync(int barberId, bool isActive, int? changedByUserId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(@"UPDATE Barbers
                                                        SET IsActive = @IsActive
                                                        WHERE BarberId = @BarberId", connection))
                    {
                        command.Parameters.AddWithValue("@BarberId", barberId);
                        command.Parameters.AddWithValue("@IsActive", isActive);
                        var rows = await command.ExecuteNonQueryAsync();
                        Debug.WriteLine($"[DatabaseService] Updating barber status: {barberId} to {isActive}");
                        return rows > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] UpdateBarberStatusAsync failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteBarberAsync(int barberId, int? changedByUserId = null)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand("DELETE FROM Barbers WHERE BarberId = @BarberId", connection))
                    {
                        command.Parameters.AddWithValue("@BarberId", barberId);
                        var rows = await command.ExecuteNonQueryAsync();
                        Debug.WriteLine($"[DatabaseService] Deleting barber: {barberId}");
                        return rows > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] DeleteBarberAsync failed: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region Booking Operations
        public async Task<List<Booking>> GetBookingsAsync()
        {
            try
            {
                var bookings = new List<Booking>();

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    try
                    {
                        var queryWithCreatedBy = @"SELECT BookingId, CustomerName, ServiceId, BarberId, TotalAmount, DepositAmount, Schedule, Status, CreatedDate, CreatedByUserId
                                                  FROM Bookings
                                                  ORDER BY CreatedDate DESC";

                        using (var command = new SqlCommand(queryWithCreatedBy, connection))
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var createdDate = reader.GetDateTime(8);
                                bookings.Add(new Booking
                                {
                                    BookingId = reader.GetInt32(0),
                                    CustomerName = reader.GetString(1),
                                    ServiceId = reader.GetInt32(2),
                                    BarberId = reader.GetInt32(3),
                                    TotalAmount = reader.GetDecimal(4),
                                    DepositAmount = reader.GetDecimal(5),
                                    Schedule = reader.GetDateTime(6),
                                    Status = reader.GetString(7),
                                    CreatedDate = createdDate,
                                    BookingDate = createdDate,
                                    CreatedByUserId = reader.IsDBNull(9) ? null : reader.GetInt32(9)
                                });
                            }
                        }
                    }
                    catch (SqlException ex) when (IsInvalidColumnName(ex, "CreatedByUserId"))
                    {
                        var query = @"SELECT BookingId, CustomerName, ServiceId, BarberId, TotalAmount, DepositAmount, Schedule, Status, CreatedDate
                                      FROM Bookings
                                      ORDER BY CreatedDate DESC";

                        using (var command = new SqlCommand(query, connection))
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var createdDate = reader.GetDateTime(8);
                                bookings.Add(new Booking
                                {
                                    BookingId = reader.GetInt32(0),
                                    CustomerName = reader.GetString(1),
                                    ServiceId = reader.GetInt32(2),
                                    BarberId = reader.GetInt32(3),
                                    TotalAmount = reader.GetDecimal(4),
                                    DepositAmount = reader.GetDecimal(5),
                                    Schedule = reader.GetDateTime(6),
                                    Status = reader.GetString(7),
                                    CreatedDate = createdDate,
                                    BookingDate = createdDate,
                                    CreatedByUserId = null
                                });
                            }
                        }
                    }
                }

                Debug.WriteLine("[DatabaseService] Getting bookings");
                return bookings;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] GetBookingsAsync failed: {ex.Message}");
                return new List<Booking>();
            }
        }

        public async Task<int> CreateBookingAsync(string customerName, int serviceId, int barberId, decimal totalAmount, decimal depositAmount, DateTime schedule, int? createdByUserId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    try
                    {
                        var queryWithCreatedBy = @"INSERT INTO Bookings (CustomerName, ServiceId, BarberId, TotalAmount, DepositAmount, Schedule, Status, CreatedByUserId)
                                                  VALUES (@CustomerName, @ServiceId, @BarberId, @TotalAmount, @DepositAmount, @Schedule, 'Pending', @CreatedByUserId);
                                                  SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        using (var command = new SqlCommand(queryWithCreatedBy, connection))
                        {
                            command.Parameters.AddWithValue("@CustomerName", customerName);
                            command.Parameters.AddWithValue("@ServiceId", serviceId);
                            command.Parameters.AddWithValue("@BarberId", barberId);
                            command.Parameters.AddWithValue("@TotalAmount", totalAmount);
                            command.Parameters.AddWithValue("@DepositAmount", depositAmount);
                            command.Parameters.AddWithValue("@Schedule", schedule);
                            command.Parameters.AddWithValue("@CreatedByUserId", (object?)createdByUserId ?? DBNull.Value);
                            var result = await command.ExecuteScalarAsync();
                            Debug.WriteLine($"[DatabaseService] Creating booking for: {customerName}");
                            return result == null ? 0 : Convert.ToInt32(result);
                        }
                    }
                    catch (SqlException ex) when (IsInvalidColumnName(ex, "CreatedByUserId"))
                    {
                        var query = @"INSERT INTO Bookings (CustomerName, ServiceId, BarberId, TotalAmount, DepositAmount, Schedule, Status)
                                      VALUES (@CustomerName, @ServiceId, @BarberId, @TotalAmount, @DepositAmount, @Schedule, 'Pending');
                                      SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        using (var command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@CustomerName", customerName);
                            command.Parameters.AddWithValue("@ServiceId", serviceId);
                            command.Parameters.AddWithValue("@BarberId", barberId);
                            command.Parameters.AddWithValue("@TotalAmount", totalAmount);
                            command.Parameters.AddWithValue("@DepositAmount", depositAmount);
                            command.Parameters.AddWithValue("@Schedule", schedule);
                            var result = await command.ExecuteScalarAsync();
                            Debug.WriteLine($"[DatabaseService] Creating booking for: {customerName}");
                            return result == null ? 0 : Convert.ToInt32(result);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] CreateBookingAsync failed: {ex.Message}");
                return 0;
            }
        }

        public async Task<bool> UpdateBookingStatusAsync(int bookingId, string status, int? changedByUserId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(@"UPDATE Bookings
                                                        SET Status = @Status
                                                        WHERE BookingId = @BookingId", connection))
                    {
                        command.Parameters.AddWithValue("@BookingId", bookingId);
                        command.Parameters.AddWithValue("@Status", status);
                        var rows = await command.ExecuteNonQueryAsync();
                        Debug.WriteLine($"[DatabaseService] Updating booking status: {bookingId} to {status}");
                        return rows > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] UpdateBookingStatusAsync failed: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region WalkIn Operations
        public async Task<List<WalkInTransaction>> GetWalkInTransactionsAsync()
        {
            try
            {
                var transactions = new List<WalkInTransaction>();

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    try
                    {
                        var query = @"SELECT WalkInId, CustomerName, ServiceId, BarberId, TotalAmount, ServiceDate, Status, CreatedDate, CreatedByUserId
                                      FROM WalkInTransactions
                                      ORDER BY CreatedDate DESC";

                        using (var command = new SqlCommand(query, connection))
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var createdDate = reader.GetDateTime(7);

                                transactions.Add(new WalkInTransaction
                                {
                                    WalkInId = reader.GetInt32(0),
                                    CustomerName = reader.GetString(1),
                                    ServiceId = reader.GetInt32(2),
                                    BarberId = reader.GetInt32(3),
                                    TotalAmount = reader.GetDecimal(4),
                                    ServiceDate = reader.GetDateTime(5),
                                    Status = reader.GetString(6),
                                    CreatedDate = createdDate,
                                    TransactionDate = createdDate,
                                    CreatedByUserId = reader.IsDBNull(8) ? null : reader.GetInt32(8)
                                });
                            }
                        }
                    }
                    catch (SqlException ex) when (IsInvalidColumnName(ex, "CreatedByUserId"))
                    {
                        var query = @"SELECT WalkInId, CustomerName, ServiceId, BarberId, TotalAmount, ServiceDate, Status, CreatedDate
                                      FROM WalkInTransactions
                                      ORDER BY CreatedDate DESC";

                        using (var command = new SqlCommand(query, connection))
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var createdDate = reader.GetDateTime(7);

                                transactions.Add(new WalkInTransaction
                                {
                                    WalkInId = reader.GetInt32(0),
                                    CustomerName = reader.GetString(1),
                                    ServiceId = reader.GetInt32(2),
                                    BarberId = reader.GetInt32(3),
                                    TotalAmount = reader.GetDecimal(4),
                                    ServiceDate = reader.GetDateTime(5),
                                    Status = reader.GetString(6),
                                    CreatedDate = createdDate,
                                    TransactionDate = createdDate,
                                    CreatedByUserId = null
                                });
                            }
                        }
                    }
                    catch (SqlException ex) when (IsInvalidColumnName(ex, "ServiceDate"))
                    {
                        var query = @"SELECT WalkInId, CustomerName, ServiceId, BarberId, TotalAmount, DateTime, Status, CreatedDate
                                      FROM WalkInTransactions
                                      ORDER BY CreatedDate DESC";

                        using (var command = new SqlCommand(query, connection))
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var createdDate = reader.GetDateTime(7);
                                var serviceDate = reader.GetDateTime(5);

                                transactions.Add(new WalkInTransaction
                                {
                                    WalkInId = reader.GetInt32(0),
                                    CustomerName = reader.GetString(1),
                                    ServiceId = reader.GetInt32(2),
                                    BarberId = reader.GetInt32(3),
                                    TotalAmount = reader.GetDecimal(4),
                                    ServiceDate = serviceDate,
                                    Status = reader.GetString(6),
                                    CreatedDate = createdDate,
                                    TransactionDate = createdDate,
                                    CreatedByUserId = null
                                });
                            }
                        }
                    }
                }

                Debug.WriteLine("[DatabaseService] Getting walk-in transactions");
                return transactions;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] GetWalkInTransactionsAsync failed: {ex.Message}");
                return new List<WalkInTransaction>();
            }
        }

        public async Task<int> CreateWalkInTransactionAsync(string customerName, int serviceId, int barberId, decimal totalAmount, int? createdByUserId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    try
                    {
                        var query = @"INSERT INTO WalkInTransactions (CustomerName, ServiceId, BarberId, TotalAmount, ServiceDate, Status, CreatedByUserId)
                                      VALUES (@CustomerName, @ServiceId, @BarberId, @TotalAmount, GETDATE(), 'Paid', @CreatedByUserId);
                                      SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        using (var command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@CustomerName", customerName);
                            command.Parameters.AddWithValue("@ServiceId", serviceId);
                            command.Parameters.AddWithValue("@BarberId", barberId);
                            command.Parameters.AddWithValue("@TotalAmount", totalAmount);
                            command.Parameters.AddWithValue("@CreatedByUserId", (object?)createdByUserId ?? DBNull.Value);
                            var result = await command.ExecuteScalarAsync();
                            Debug.WriteLine($"[DatabaseService] Creating walk-in transaction for: {customerName}");
                            return result == null ? 0 : Convert.ToInt32(result);
                        }
                    }
                    catch (SqlException ex) when (IsInvalidColumnName(ex, "CreatedByUserId"))
                    {
                        var query = @"INSERT INTO WalkInTransactions (CustomerName, ServiceId, BarberId, TotalAmount, ServiceDate, Status)
                                      VALUES (@CustomerName, @ServiceId, @BarberId, @TotalAmount, GETDATE(), 'Paid');
                                      SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        using (var command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@CustomerName", customerName);
                            command.Parameters.AddWithValue("@ServiceId", serviceId);
                            command.Parameters.AddWithValue("@BarberId", barberId);
                            command.Parameters.AddWithValue("@TotalAmount", totalAmount);
                            var result = await command.ExecuteScalarAsync();
                            Debug.WriteLine($"[DatabaseService] Creating walk-in transaction for: {customerName}");
                            return result == null ? 0 : Convert.ToInt32(result);
                        }
                    }
                    catch (SqlException ex) when (IsInvalidColumnName(ex, "ServiceDate"))
                    {
                        var query = @"INSERT INTO WalkInTransactions (CustomerName, ServiceId, BarberId, TotalAmount, DateTime, Status)
                                      VALUES (@CustomerName, @ServiceId, @BarberId, @TotalAmount, GETDATE(), 'Paid');
                                      SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        using (var command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@CustomerName", customerName);
                            command.Parameters.AddWithValue("@ServiceId", serviceId);
                            command.Parameters.AddWithValue("@BarberId", barberId);
                            command.Parameters.AddWithValue("@TotalAmount", totalAmount);
                            var result = await command.ExecuteScalarAsync();
                            Debug.WriteLine($"[DatabaseService] Creating walk-in transaction for: {customerName}");
                            return result == null ? 0 : Convert.ToInt32(result);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] CreateWalkInTransactionAsync failed: {ex.Message}");
                return 0;
            }
        }

        public async Task<bool> UpdateWalkInStatusAsync(int walkInId, string status, int? changedByUserId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(@"UPDATE WalkInTransactions
                                                        SET Status = @Status
                                                        WHERE WalkInId = @WalkInId", connection))
                    {
                        command.Parameters.AddWithValue("@WalkInId", walkInId);
                        command.Parameters.AddWithValue("@Status", status);
                        var rows = await command.ExecuteNonQueryAsync();
                        Debug.WriteLine($"[DatabaseService] Updating walk-in status: {walkInId} to {status}");
                        return rows > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] UpdateWalkInStatusAsync failed: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region User Operations
        public async Task<bool> UpdateUserActiveStatusAsync(int userId, bool isActive, int? changedByUserId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(@"UPDATE Users
                                                        SET IsActive = @IsActive
                                                        WHERE UserId = @UserId", connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);
                        command.Parameters.AddWithValue("@IsActive", isActive);
                        var rows = await command.ExecuteNonQueryAsync();
                        Debug.WriteLine($"[DatabaseService] Updating user active status: {userId} to {isActive}");
                        return rows > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] UpdateUserActiveStatusAsync failed: {ex.Message}");
                return false;
            }
        }
        #endregion
    }
}
