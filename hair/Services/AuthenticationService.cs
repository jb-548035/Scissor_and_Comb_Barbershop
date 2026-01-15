using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Maui.Networking;

namespace hair.Services
{
    public class AuthenticationService
    {
        private readonly string _connectionString;
        private readonly string _localConnectionString;
        private bool IsOnline => Connectivity.NetworkAccess == NetworkAccess.Internet;

        private static bool IsInvalidColumnName(SqlException ex, string columnName)
        {
            return ex.Message.IndexOf($"Invalid column name '{columnName}'", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public AuthenticationService(string serverName, string databaseName, string userId = "", string password = "")
        {
            // Build connection string
            if (string.IsNullOrEmpty(userId))
            {
                // Windows Authentication
                _connectionString = $"Server={serverName};Database={databaseName};Integrated Security=true;TrustServerCertificate=true;";
            }
            else
            {
                // SQL Authentication
                _connectionString = $"Server={serverName};Database={databaseName};User Id={userId};Password={password};TrustServerCertificate=true;";
            }

            _localConnectionString = "";
        }

        public AuthenticationService(
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

        /// <summary>
        /// Verify password against hash
        /// </summary>
        private bool VerifyPassword(string password, string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
                return false;

            var stored = hash.Trim();

            if (stored.Equals(password, StringComparison.Ordinal))
                return true;

            var hashOfInputBase64 = HashPassword(password);
            if (hashOfInputBase64.Equals(stored, StringComparison.Ordinal))
                return true;

            var hashOfInputHex = HashPasswordHex(password);
            if (hashOfInputHex.Equals(stored, StringComparison.OrdinalIgnoreCase))
                return true;

            if (stored.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return ("0x" + hashOfInputHex).Equals(stored, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private string HashPasswordHex(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                var sb = new StringBuilder(hashedBytes.Length * 2);
                for (int i = 0; i < hashedBytes.Length; i++)
                {
                    sb.Append(hashedBytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// Register a new user
        /// </summary>
        public async Task<(bool success, string message, int userId)> RegisterAsync(string firstName, string lastName, string email, string phone, string role, string password)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
                    return (false, "First name and last name are required", -1);

                if (string.IsNullOrWhiteSpace(email))
                    return (false, "Email is required", -1);

                // Validate password strength
                if (string.IsNullOrWhiteSpace(password))
                    return (false, "Password is required", -1);

                if (password.Length < 12)
                    return (false, "Password must be at least 12 characters", -1);

                if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"[a-zA-Z]"))
                    return (false, "Password must contain at least one letter", -1);

                if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"[0-9]"))
                    return (false, "Password must contain at least one number", -1);

                if (string.IsNullOrWhiteSpace(role))
                    return (false, "Role is required", -1);

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Check if email already exists
                    string checkQuery = "SELECT COUNT(*) FROM Users WHERE Email = @Email";
                    using (var checkCommand = new SqlCommand(checkQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@Email", email);
                        var count = (int)await checkCommand.ExecuteScalarAsync();
                        if (count > 0)
                            return (false, "Email already registered", -1);
                    }

                    // Hash password
                    string passwordHash = HashPassword(password);

                    // Insert new user
                    string insertQuery = @"INSERT INTO Users (FirstName, LastName, Email, Phone, Role, PasswordHash, CreatedDate, IsActive) 
                                          VALUES (@FirstName, @LastName, @Email, @Phone, @Role, @PasswordHash, GETDATE(), 1);
                                          SELECT SCOPE_IDENTITY();";

                    using (var insertCommand = new SqlCommand(insertQuery, connection))
                    {
                        insertCommand.Parameters.AddWithValue("@FirstName", firstName);
                        insertCommand.Parameters.AddWithValue("@LastName", lastName);
                        insertCommand.Parameters.AddWithValue("@Email", email);
                        insertCommand.Parameters.AddWithValue("@Phone", phone ?? "");
                        insertCommand.Parameters.AddWithValue("@Role", role);
                        insertCommand.Parameters.AddWithValue("@PasswordHash", passwordHash);

                        var result = await insertCommand.ExecuteScalarAsync();
                        int userId = Convert.ToInt32(result);

                        await TryCacheUserToLocalAsync(new UserRecord
                        {
                            UserId = userId,
                            FirstName = firstName,
                            LastName = lastName,
                            Email = email,
                            Phone = phone ?? "",
                            Role = role,
                            PasswordHash = passwordHash,
                            IsActive = true,
                            RequiresPasswordChange = false
                        });

                        System.Diagnostics.Debug.WriteLine($"[AuthenticationService] User registered successfully. UserId: {userId}");
                        return (true, "Account created successfully", userId);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AuthenticationService] Registration error: {ex.Message}");
                return (false, "An error occurred during registration", -1);
            }
        }

        /// <summary>
        /// Login user with email and password
        /// </summary>
        public async Task<(bool success, string message, UserInfo user)> LoginAsync(string email, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                    return (false, "Email and password are required", null);

                email = email.Trim();

                if (!string.IsNullOrWhiteSpace(_localConnectionString))
                {
                    var localAttempt = await TryLoginInternalAsync(_localConnectionString, email, password);
                    if (localAttempt.exception == null && localAttempt.success && localAttempt.record != null)
                    {
                        if (IsOnline)
                        {
                            var cloudAttempt = await TryLoginInternalAsync(_connectionString, email, password);
                            if (cloudAttempt.exception == null && cloudAttempt.success && cloudAttempt.record != null)
                            {
                                await TryCacheUserToLocalAsync(cloudAttempt.record);
                                return (true, "Login successful", cloudAttempt.record.ToUserInfo());
                            }

                            return (true, "Login successful (local)", localAttempt.record.ToUserInfo());
                        }

                        return (true, "Login successful (offline)", localAttempt.record.ToUserInfo());
                    }

                    if (!IsOnline)
                    {
                        return (false, "Invalid email or password", null);
                    }
                }

                if (IsOnline)
                {
                    var cloudAttempt = await TryLoginInternalAsync(_connectionString, email, password);
                    if (cloudAttempt.exception == null)
                    {
                        if (cloudAttempt.success && cloudAttempt.record != null)
                        {
                            await TryCacheUserToLocalAsync(cloudAttempt.record);
                            return (true, "Login successful", cloudAttempt.record.ToUserInfo());
                        }

                        return (false, cloudAttempt.message, null);
                    }

                    System.Diagnostics.Debug.WriteLine($"[AuthenticationService] Cloud login failed: {cloudAttempt.exception.Message}, trying local");
                }

                if (string.IsNullOrWhiteSpace(_localConnectionString))
                    return (false, "No local database configured for offline login", null);

                var localFallbackAttempt = await TryLoginInternalAsync(_localConnectionString, email, password);
                if (localFallbackAttempt.success && localFallbackAttempt.record != null)
                {
                    var message = IsOnline ? "Login successful" : "Login successful (offline)";
                    return (true, message, localFallbackAttempt.record.ToUserInfo());
                }

                return (false, localFallbackAttempt.message, null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AuthenticationService] Login error: {ex.Message}");
                return (false, "An error occurred during login", null);
            }
        }

        public async Task WarmLocalUserCacheFromCloudAsync()
        {
            try
            {
                if (!IsOnline)
                    return;

                if (string.IsNullOrWhiteSpace(_localConnectionString))
                    return;

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var queryWithPasswordChange = @"SELECT UserId, FirstName, LastName, Email, Phone, Role, PasswordHash, IsActive, RequiresPasswordChange
                                    FROM Users";

                    try
                    {
                        using (var command = new SqlCommand(queryWithPasswordChange, connection))
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var record = new UserRecord
                                {
                                    UserId = reader.GetInt32(0),
                                    FirstName = reader.GetString(1),
                                    LastName = reader.GetString(2),
                                    Email = reader.GetString(3),
                                    Phone = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    Role = reader.GetString(5),
                                    PasswordHash = reader.GetString(6),
                                    IsActive = reader.GetBoolean(7),
                                    RequiresPasswordChange = reader.GetBoolean(8)
                                };

                                await TryCacheUserToLocalAsync(record);
                            }
                        }
                    }
                    catch (SqlException ex) when (IsInvalidColumnName(ex, "RequiresPasswordChange"))
                    {
                        var queryWithoutPasswordChange = @"SELECT UserId, FirstName, LastName, Email, Phone, Role, PasswordHash, IsActive
                                    FROM Users";

                        using (var command = new SqlCommand(queryWithoutPasswordChange, connection))
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var record = new UserRecord
                                {
                                    UserId = reader.GetInt32(0),
                                    FirstName = reader.GetString(1),
                                    LastName = reader.GetString(2),
                                    Email = reader.GetString(3),
                                    Phone = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    Role = reader.GetString(5),
                                    PasswordHash = reader.GetString(6),
                                    IsActive = reader.GetBoolean(7),
                                    RequiresPasswordChange = false
                                };

                                await TryCacheUserToLocalAsync(record);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AuthenticationService] Failed to warm local user cache: {ex.Message}");
            }
        }

        private sealed class UserRecord
        {
            public int UserId { get; init; }
            public string FirstName { get; init; } = "";
            public string LastName { get; init; } = "";
            public string Email { get; init; } = "";
            public string Phone { get; init; } = "";
            public string Role { get; init; } = "";
            public string PasswordHash { get; init; } = "";
            public bool IsActive { get; init; }
            public bool RequiresPasswordChange { get; init; }

            public UserInfo ToUserInfo()
            {
                return new UserInfo
                {
                    UserId = UserId,
                    FirstName = FirstName,
                    LastName = LastName,
                    Email = Email,
                    Phone = Phone,
                    Role = Role,
                    IsLoggedIn = true,
                    RequiresPasswordChange = RequiresPasswordChange
                };
            }
        }

        private async Task<(bool success, string message, UserRecord record, Exception exception)> TryLoginInternalAsync(string connectionString, string email, string password)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    var queryWithPasswordChange = @"SELECT UserId, FirstName, LastName, Email, Phone, Role, PasswordHash, IsActive, RequiresPasswordChange 
                                    FROM Users 
                                    WHERE LOWER(LTRIM(RTRIM(Email))) = LOWER(@Email)";

                    try
                    {
                        using (var command = new SqlCommand(queryWithPasswordChange, connection))
                        {
                            command.Parameters.AddWithValue("@Email", email);

                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                if (!await reader.ReadAsync())
                                    return (false, "Invalid email or password", null, null);

                                int userId = reader.GetInt32(0);
                                string firstName = reader.GetString(1);
                                string lastName = reader.GetString(2);
                                string dbEmail = reader.GetString(3);
                                string phone = reader.IsDBNull(4) ? "" : reader.GetString(4);
                                string role = reader.GetString(5);
                                string passwordHash = reader.GetString(6);
                                bool isActive = reader.GetBoolean(7);
                                bool requiresPasswordChange = reader.GetBoolean(8);

                                if (!isActive)
                                    return (false, "Account is inactive", null, null);

                                if (!VerifyPassword(password, passwordHash))
                                    return (false, "Invalid email or password", null, null);

                                var record = new UserRecord
                                {
                                    UserId = userId,
                                    FirstName = firstName,
                                    LastName = lastName,
                                    Email = dbEmail,
                                    Phone = phone,
                                    Role = role,
                                    PasswordHash = passwordHash,
                                    IsActive = isActive,
                                    RequiresPasswordChange = requiresPasswordChange
                                };

                                return (true, "Login successful", record, null);
                            }
                        }
                    }
                    catch (SqlException ex) when (IsInvalidColumnName(ex, "RequiresPasswordChange"))
                    {
                        var queryWithoutPasswordChange = @"SELECT UserId, FirstName, LastName, Email, Phone, Role, PasswordHash, IsActive 
                                    FROM Users 
                                    WHERE LOWER(LTRIM(RTRIM(Email))) = LOWER(@Email)";

                        using (var command = new SqlCommand(queryWithoutPasswordChange, connection))
                        {
                            command.Parameters.AddWithValue("@Email", email);

                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                if (!await reader.ReadAsync())
                                    return (false, "Invalid email or password", null, null);

                                int userId = reader.GetInt32(0);
                                string firstName = reader.GetString(1);
                                string lastName = reader.GetString(2);
                                string dbEmail = reader.GetString(3);
                                string phone = reader.IsDBNull(4) ? "" : reader.GetString(4);
                                string role = reader.GetString(5);
                                string passwordHash = reader.GetString(6);
                                bool isActive = reader.GetBoolean(7);

                                if (!isActive)
                                    return (false, "Account is inactive", null, null);

                                if (!VerifyPassword(password, passwordHash))
                                    return (false, "Invalid email or password", null, null);

                                var record = new UserRecord
                                {
                                    UserId = userId,
                                    FirstName = firstName,
                                    LastName = lastName,
                                    Email = dbEmail,
                                    Phone = phone,
                                    Role = role,
                                    PasswordHash = passwordHash,
                                    IsActive = isActive,
                                    RequiresPasswordChange = false
                                };

                                return (true, "Login successful", record, null);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, "An error occurred during login", null, ex);
            }
        }

        private async Task TryCacheUserToLocalAsync(UserRecord record)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_localConnectionString))
                    return;

                using (var connection = new SqlConnection(_localConnectionString))
                {
                    await connection.OpenAsync();

                    var existsQuery = "SELECT COUNT(*) FROM Users WHERE Email = @Email";
                    using (var existsCommand = new SqlCommand(existsQuery, connection))
                    {
                        existsCommand.Parameters.AddWithValue("@Email", record.Email);
                        var exists = Convert.ToInt32(await existsCommand.ExecuteScalarAsync()) > 0;

                        if (exists)
                        {
                            var updateQuery = @"UPDATE Users
                                               SET FirstName = @FirstName,
                                                   LastName = @LastName,
                                                   Phone = @Phone,
                                                   Role = @Role,
                                                   PasswordHash = @PasswordHash,
                                                   IsActive = @IsActive,
                                                   RequiresPasswordChange = @RequiresPasswordChange
                                               WHERE Email = @Email";

                            try
                            {
                                using (var updateCommand = new SqlCommand(updateQuery, connection))
                                {
                                    updateCommand.Parameters.AddWithValue("@FirstName", record.FirstName);
                                    updateCommand.Parameters.AddWithValue("@LastName", record.LastName);
                                    updateCommand.Parameters.AddWithValue("@Phone", record.Phone ?? "");
                                    updateCommand.Parameters.AddWithValue("@Role", record.Role);
                                    updateCommand.Parameters.AddWithValue("@PasswordHash", record.PasswordHash);
                                    updateCommand.Parameters.AddWithValue("@IsActive", record.IsActive);
                                    updateCommand.Parameters.AddWithValue("@RequiresPasswordChange", record.RequiresPasswordChange);
                                    updateCommand.Parameters.AddWithValue("@Email", record.Email);
                                    await updateCommand.ExecuteNonQueryAsync();
                                }
                            }
                            catch (SqlException ex) when (IsInvalidColumnName(ex, "RequiresPasswordChange"))
                            {
                                var updateQueryWithoutPasswordChange = @"UPDATE Users
                                               SET FirstName = @FirstName,
                                                   LastName = @LastName,
                                                   Phone = @Phone,
                                                   Role = @Role,
                                                   PasswordHash = @PasswordHash,
                                                   IsActive = @IsActive
                                               WHERE Email = @Email";

                                using (var updateCommand = new SqlCommand(updateQueryWithoutPasswordChange, connection))
                                {
                                    updateCommand.Parameters.AddWithValue("@FirstName", record.FirstName);
                                    updateCommand.Parameters.AddWithValue("@LastName", record.LastName);
                                    updateCommand.Parameters.AddWithValue("@Phone", record.Phone ?? "");
                                    updateCommand.Parameters.AddWithValue("@Role", record.Role);
                                    updateCommand.Parameters.AddWithValue("@PasswordHash", record.PasswordHash);
                                    updateCommand.Parameters.AddWithValue("@IsActive", record.IsActive);
                                    updateCommand.Parameters.AddWithValue("@Email", record.Email);
                                    await updateCommand.ExecuteNonQueryAsync();
                                }
                            }

                            return;
                        }
                    }

                    try
                    {
                        var insertWithIdQuery = @"SET IDENTITY_INSERT Users ON;
                                                 INSERT INTO Users (UserId, FirstName, LastName, Email, Phone, Role, PasswordHash, CreatedDate, IsActive, RequiresPasswordChange)
                                                 VALUES (@UserId, @FirstName, @LastName, @Email, @Phone, @Role, @PasswordHash, GETDATE(), @IsActive, @RequiresPasswordChange);
                                                 SET IDENTITY_INSERT Users OFF;";

                        try
                        {
                            using (var insertCommand = new SqlCommand(insertWithIdQuery, connection))
                            {
                                insertCommand.Parameters.AddWithValue("@UserId", record.UserId);
                                insertCommand.Parameters.AddWithValue("@FirstName", record.FirstName);
                                insertCommand.Parameters.AddWithValue("@LastName", record.LastName);
                                insertCommand.Parameters.AddWithValue("@Email", record.Email);
                                insertCommand.Parameters.AddWithValue("@Phone", record.Phone ?? "");
                                insertCommand.Parameters.AddWithValue("@Role", record.Role);
                                insertCommand.Parameters.AddWithValue("@PasswordHash", record.PasswordHash);
                                insertCommand.Parameters.AddWithValue("@IsActive", record.IsActive);
                                insertCommand.Parameters.AddWithValue("@RequiresPasswordChange", record.RequiresPasswordChange);
                                await insertCommand.ExecuteNonQueryAsync();
                            }
                        }
                        catch (SqlException ex) when (IsInvalidColumnName(ex, "RequiresPasswordChange"))
                        {
                            var insertWithIdQueryWithoutPasswordChange = @"SET IDENTITY_INSERT Users ON;
                                                 INSERT INTO Users (UserId, FirstName, LastName, Email, Phone, Role, PasswordHash, CreatedDate, IsActive)
                                                 VALUES (@UserId, @FirstName, @LastName, @Email, @Phone, @Role, @PasswordHash, GETDATE(), @IsActive);
                                                 SET IDENTITY_INSERT Users OFF;";

                            using (var insertCommand = new SqlCommand(insertWithIdQueryWithoutPasswordChange, connection))
                            {
                                insertCommand.Parameters.AddWithValue("@UserId", record.UserId);
                                insertCommand.Parameters.AddWithValue("@FirstName", record.FirstName);
                                insertCommand.Parameters.AddWithValue("@LastName", record.LastName);
                                insertCommand.Parameters.AddWithValue("@Email", record.Email);
                                insertCommand.Parameters.AddWithValue("@Phone", record.Phone ?? "");
                                insertCommand.Parameters.AddWithValue("@Role", record.Role);
                                insertCommand.Parameters.AddWithValue("@PasswordHash", record.PasswordHash);
                                insertCommand.Parameters.AddWithValue("@IsActive", record.IsActive);
                                await insertCommand.ExecuteNonQueryAsync();
                            }
                        }
                    }
                    catch (Exception)
                    {
                        var insertQuery = @"INSERT INTO Users (FirstName, LastName, Email, Phone, Role, PasswordHash, CreatedDate, IsActive, RequiresPasswordChange)
                                            VALUES (@FirstName, @LastName, @Email, @Phone, @Role, @PasswordHash, GETDATE(), @IsActive, @RequiresPasswordChange);";

                        try
                        {
                            using (var insertCommand = new SqlCommand(insertQuery, connection))
                            {
                                insertCommand.Parameters.AddWithValue("@FirstName", record.FirstName);
                                insertCommand.Parameters.AddWithValue("@LastName", record.LastName);
                                insertCommand.Parameters.AddWithValue("@Email", record.Email);
                                insertCommand.Parameters.AddWithValue("@Phone", record.Phone ?? "");
                                insertCommand.Parameters.AddWithValue("@Role", record.Role);
                                insertCommand.Parameters.AddWithValue("@PasswordHash", record.PasswordHash);
                                insertCommand.Parameters.AddWithValue("@IsActive", record.IsActive);
                                insertCommand.Parameters.AddWithValue("@RequiresPasswordChange", record.RequiresPasswordChange);
                                await insertCommand.ExecuteNonQueryAsync();
                            }
                        }
                        catch (SqlException ex) when (IsInvalidColumnName(ex, "RequiresPasswordChange"))
                        {
                            var insertQueryWithoutPasswordChange = @"INSERT INTO Users (FirstName, LastName, Email, Phone, Role, PasswordHash, CreatedDate, IsActive)
                                            VALUES (@FirstName, @LastName, @Email, @Phone, @Role, @PasswordHash, GETDATE(), @IsActive);";

                            using (var insertCommand = new SqlCommand(insertQueryWithoutPasswordChange, connection))
                            {
                                insertCommand.Parameters.AddWithValue("@FirstName", record.FirstName);
                                insertCommand.Parameters.AddWithValue("@LastName", record.LastName);
                                insertCommand.Parameters.AddWithValue("@Email", record.Email);
                                insertCommand.Parameters.AddWithValue("@Phone", record.Phone ?? "");
                                insertCommand.Parameters.AddWithValue("@Role", record.Role);
                                insertCommand.Parameters.AddWithValue("@PasswordHash", record.PasswordHash);
                                insertCommand.Parameters.AddWithValue("@IsActive", record.IsActive);
                                await insertCommand.ExecuteNonQueryAsync();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AuthenticationService] Failed to cache user locally: {ex.Message}");
            }
        }

        /// <summary>
        /// Get user by ID
        /// </summary>
        public async Task<UserInfo> GetUserByIdAsync(int userId)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_localConnectionString))
                {
                    try
                    {
                        var local = await GetUserByIdInternalAsync(_localConnectionString, userId);
                        if (local != null)
                            return local;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AuthenticationService] Local GetUserById failed: {ex.Message}");
                    }
                }

                if (IsOnline)
                {
                    return await GetUserByIdInternalAsync(_connectionString, userId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AuthenticationService] Error getting user: {ex.Message}");
            }

            return null;
        }

        private async Task<UserInfo> GetUserByIdInternalAsync(string connectionString, int userId)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                string query = @"SELECT UserId, FirstName, LastName, Email, Phone, Role, IsActive 
                                    FROM Users 
                                    WHERE UserId = @UserId";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserId", userId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new UserInfo
                            {
                                UserId = reader.GetInt32(0),
                                FirstName = reader.GetString(1),
                                LastName = reader.GetString(2),
                                Email = reader.GetString(3),
                                Phone = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                Role = reader.GetString(5),
                                IsLoggedIn = reader.GetBoolean(6)
                            };
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Change user password
        /// </summary>
        public async Task<(bool success, string message)> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
                    return (false, "Current and new passwords are required");

                if (!IsOnline && !string.IsNullOrWhiteSpace(_localConnectionString))
                {
                    return await ChangePasswordInternalAsync(_localConnectionString, userId, currentPassword, newPassword);
                }

                var primaryConnectionString = IsOnline ? _connectionString : _localConnectionString;
                if (string.IsNullOrWhiteSpace(primaryConnectionString))
                    return (false, "No database configured for password change");

                var primaryResult = await ChangePasswordInternalAsync(primaryConnectionString, userId, currentPassword, newPassword);

                if (primaryResult.success && !string.IsNullOrWhiteSpace(_localConnectionString) && primaryConnectionString != _localConnectionString)
                {
                    try
                    {
                        await ChangePasswordInternalAsync(_localConnectionString, userId, currentPassword, newPassword);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AuthenticationService] Failed to update local password: {ex.Message}");
                    }
                }

                return primaryResult;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AuthenticationService] Change password error: {ex.Message}");
                return (false, "An error occurred while changing password");
            }
        }

        private async Task<(bool success, string message)> ChangePasswordInternalAsync(string connectionString, int userId, string currentPassword, string newPassword)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                string query = "SELECT PasswordHash FROM Users WHERE UserId = @UserId";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserId", userId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            string passwordHash = reader.GetString(0);

                            if (!VerifyPassword(currentPassword, passwordHash))
                                return (false, "Current password is incorrect");
                        }
                        else
                        {
                            return (false, "User not found");
                        }
                    }
                }

                string newPasswordHash = HashPassword(newPassword);

                string updateQuery = @"UPDATE Users 
                                          SET PasswordHash = @PasswordHash, RequiresPasswordChange = 0 
                                          WHERE UserId = @UserId";

                try
                {
                    using (var command = new SqlCommand(updateQuery, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);
                        command.Parameters.AddWithValue("@PasswordHash", newPasswordHash);

                        await command.ExecuteNonQueryAsync();
                        System.Diagnostics.Debug.WriteLine($"[AuthenticationService] Password changed successfully for UserId: {userId}");
                        return (true, "Password changed successfully");
                    }
                }
                catch (SqlException ex) when (IsInvalidColumnName(ex, "RequiresPasswordChange"))
                {
                    string updateQueryWithoutPasswordChange = @"UPDATE Users 
                                          SET PasswordHash = @PasswordHash
                                          WHERE UserId = @UserId";

                    using (var command = new SqlCommand(updateQueryWithoutPasswordChange, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);
                        command.Parameters.AddWithValue("@PasswordHash", newPasswordHash);

                        await command.ExecuteNonQueryAsync();
                        System.Diagnostics.Debug.WriteLine($"[AuthenticationService] Password changed successfully for UserId: {userId}");
                        return (true, "Password changed successfully");
                    }
                }
            }
        }

        /// <summary>
        /// Get all users from database
        /// </summary>
        public async Task<List<UserInfo>> GetAllUsersAsync()
        {
            var users = new List<UserInfo>();

            try
            {
                if (!string.IsNullOrWhiteSpace(_localConnectionString))
                {
                    try
                    {
                        return await GetAllUsersInternalAsync(_localConnectionString);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AuthenticationService] Local GetAllUsers failed: {ex.Message}");
                    }
                }

                if (IsOnline)
                {
                    return await GetAllUsersInternalAsync(_connectionString);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AuthenticationService] Error getting all users: {ex.Message}");
            }

            return users;
        }

        private async Task<List<UserInfo>> GetAllUsersInternalAsync(string connectionString)
        {
            var users = new List<UserInfo>();

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                string query = @"SELECT UserId, FirstName, LastName, Email, Phone, Role, IsActive 
                                    FROM Users 
                                    ORDER BY FirstName";

                using (var command = new SqlCommand(query, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            users.Add(new UserInfo
                            {
                                UserId = reader.GetInt32(0),
                                FirstName = reader.GetString(1),
                                LastName = reader.GetString(2),
                                Email = reader.GetString(3),
                                Phone = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                Role = reader.GetString(5),
                                IsLoggedIn = reader.GetBoolean(6)
                            });
                        }
                    }
                }
            }

            return users;
        }
    }

    /// <summary>
    /// User information class
    /// </summary>
    public class UserInfo
    {
        public int UserId { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Role { get; set; } = "";
        public bool IsLoggedIn { get; set; } = false;
        public bool RequiresPasswordChange { get; set; } = false;

        public string FullName => $"{FirstName} {LastName}";
    }
}
