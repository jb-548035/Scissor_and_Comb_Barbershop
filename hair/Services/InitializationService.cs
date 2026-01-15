using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace hair.Services
{
    public class InitializationService
    {
        private readonly string _connectionString;
        private readonly string _localConnectionString;

        public InitializationService(string serverName, string databaseName, string userId = "", string password = "")
        {
            if (string.IsNullOrEmpty(userId))
            {
                _connectionString = $"Server={serverName};Database={databaseName};Integrated Security=true;TrustServerCertificate=true;";
            }
            else
            {
                _connectionString = $"Server={serverName};Database={databaseName};User Id={userId};Password={password};TrustServerCertificate=true;";
            }

            _localConnectionString = "";
        }

        public InitializationService(
            string serverName,
            string databaseName,
            string userId,
            string password,
            string localServerName,
            string localDatabaseName,
            string localUserId = "",
            string localPassword = "")
        {
            if (string.IsNullOrEmpty(userId))
            {
                _connectionString = $"Server={serverName};Database={databaseName};Integrated Security=true;TrustServerCertificate=true;";
            }
            else
            {
                _connectionString = $"Server={serverName};Database={databaseName};User Id={userId};Password={password};TrustServerCertificate=true;";
            }

            if (string.IsNullOrEmpty(localUserId))
            {
                _localConnectionString = $"Server={localServerName};Database={localDatabaseName};Integrated Security=true;TrustServerCertificate=true;";
            }
            else
            {
                _localConnectionString = $"Server={localServerName};Database={localDatabaseName};User Id={localUserId};Password={localPassword};TrustServerCertificate=true;";
            }
        }

        /// <summary>
        /// Initialize system by creating default Admin if none exists
        /// </summary>
        public async Task InitializeSystemAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[InitializationService] Starting system initialization...");

                try
                {
                    await EnsureDefaultAdminAsync(_connectionString, "cloud");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[InitializationService] Cloud initialization failed: {ex.Message}");
                }

                if (!string.IsNullOrWhiteSpace(_localConnectionString))
                {
                    try
                    {
                        await EnsureDefaultAdminAsync(_localConnectionString, "local");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[InitializationService] Local initialization failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InitializationService] Initialization error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[InitializationService] Full exception: {ex}");
            }
        }

        private async Task EnsureDefaultAdminAsync(string connectionString, string label)
        {
            // Check if any Admin exists
            bool adminExists = await AdminExistsAsync(connectionString);
            System.Diagnostics.Debug.WriteLine($"[InitializationService] Admin exists check ({label}) result: {adminExists}");

            if (!adminExists)
            {
                System.Diagnostics.Debug.WriteLine($"[InitializationService] No Admin found in {label}, creating default Admin...");
                try
                {
                    await CreateDefaultAdminAsync(connectionString);
                    System.Diagnostics.Debug.WriteLine($"[InitializationService] Default Admin created successfully in {label}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[InitializationService] Failed to create default Admin in {label}: {ex.Message}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[InitializationService] Admin already exists in {label}, skipping initialization");
            }
        }

        /// <summary>
        /// Check if Admin user exists in database
        /// </summary>
        private async Task<bool> AdminExistsAsync(string connectionString)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[InitializationService] Attempting to connect with connection string: {connectionString}");
                using (var connection = new SqlConnection(connectionString))
                {
                    System.Diagnostics.Debug.WriteLine("[InitializationService] Opening database connection...");
                    await connection.OpenAsync();
                    System.Diagnostics.Debug.WriteLine("[InitializationService] Connection opened successfully");

                    string query = "SELECT COUNT(*) FROM Users WHERE Role = 'Admin'";
                    using (var command = new SqlCommand(query, connection))
                    {
                        System.Diagnostics.Debug.WriteLine("[InitializationService] Executing query to check for Admin...");
                        var count = (int)await command.ExecuteScalarAsync();
                        System.Diagnostics.Debug.WriteLine($"[InitializationService] Admin count: {count}");
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InitializationService] Error checking admin existence: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[InitializationService] Exception type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"[InitializationService] Full exception: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Create default Admin account
        /// </summary>
        private async Task CreateDefaultAdminAsync()
        {
            try
            {
                await CreateDefaultAdminAsync(_connectionString);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InitializationService] Error creating default admin: {ex.Message}");
                throw;
            }
        }

        private async Task CreateDefaultAdminAsync(string connectionString)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                string passwordHash = HashPassword("Admin@123456");

                string insertQuery = @"INSERT INTO Users (FirstName, LastName, Email, Phone, Role, PasswordHash, CreatedDate, IsActive) 
                                          VALUES (@FirstName, @LastName, @Email, @Phone, @Role, @PasswordHash, GETDATE(), 1)";

                using (var command = new SqlCommand(insertQuery, connection))
                {
                    command.Parameters.AddWithValue("@FirstName", "System");
                    command.Parameters.AddWithValue("@LastName", "Administrator");
                    command.Parameters.AddWithValue("@Email", "admin@hair.com");
                    command.Parameters.AddWithValue("@Phone", "");
                    command.Parameters.AddWithValue("@Role", "Admin");
                    command.Parameters.AddWithValue("@PasswordHash", passwordHash);

                    await command.ExecuteNonQueryAsync();
                    System.Diagnostics.Debug.WriteLine("[InitializationService] Default Admin created with email: admin@hair.com, password: Admin@123456");
                }
            }
        }

        /// <summary>
        /// Hash password using SHA256
        /// </summary>
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }
    }
}
