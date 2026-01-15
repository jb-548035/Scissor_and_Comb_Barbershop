using SQLite;
using hair.Models;
using Microsoft.Maui.Networking;
using System.Diagnostics;
using System.Text.Json;

namespace hair.Services
{
    public class SyncService
    {
        private readonly DatabaseService _cloudDb;
        private readonly string _dbPath;
        private SQLiteAsyncConnection _database;
        private readonly Task _initializationTask;

        public SyncService(DatabaseService cloudDb)
        {
            _cloudDb = cloudDb;
            _dbPath = Path.Combine(FileSystem.AppDataDirectory, "sync_queue.db");
            _initializationTask = InitializeDatabaseAsync();
        }

        private async Task InitializeDatabaseAsync()
        {
            try
            {
                _database = new SQLiteAsyncConnection(_dbPath);
                await _database.CreateTableAsync<PendingOperation>();
                Debug.WriteLine($"[SyncService] Initialized at {_dbPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SyncService] Initialization failed: {ex.Message}");
            }
        }

        private async Task EnsureInitializedAsync()
        {
            await _initializationTask;
        }

        public async Task QueueOperationAsync(string operationType, string entityType, object data)
        {
            await EnsureInitializedAsync();
            
            var operation = new PendingOperation
            {
                OperationType = operationType,
                EntityType = entityType,
                Data = JsonSerializer.Serialize(data),
                CreatedAt = DateTime.Now,
                IsSynced = false
            };

            await _database.InsertAsync(operation);
            Debug.WriteLine($"[SyncService] Queued: {operationType} {entityType}");
        }

        public async Task<(int synced, int failed)> SyncAllAsync()
        {
            await EnsureInitializedAsync();
            
            if (Connectivity.NetworkAccess != NetworkAccess.Internet)
            {
                Debug.WriteLine("[SyncService] No internet connection");
                return (0, 0);
            }

            var pending = await _database.Table<PendingOperation>()
                .Where(p => !p.IsSynced)
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();

            Debug.WriteLine($"[SyncService] Found {pending.Count} pending operations");

            int synced = 0;
            int failed = 0;

            foreach (var operation in pending)
            {
                try
                {
                    bool success = await ExecuteOperationAsync(operation);
                    if (success)
                    {
                        operation.IsSynced = true;
                        operation.SyncedAt = DateTime.Now;
                        await _database.UpdateAsync(operation);
                        synced++;
                        Debug.WriteLine($"[SyncService] Synced: {operation.OperationType} {operation.EntityType}");
                    }
                    else
                    {
                        failed++;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SyncService] Failed to sync {operation.OperationType} {operation.EntityType}: {ex.Message}");
                    failed++;
                }
            }

            // Clean up synced operations
            if (synced > 0)
            {
                await _database.ExecuteAsync("DELETE FROM PendingOperation WHERE IsSynced = 1");
            }

            Debug.WriteLine($"[SyncService] Sync complete: {synced} synced, {failed} failed");
            return (synced, failed);
        }

        private async Task<bool> ExecuteOperationAsync(PendingOperation operation)
        {
            try
            {
                switch (operation.EntityType)
                {
                    case "WalkInTransaction":
                        if (operation.OperationType == "Insert")
                        {
                            var transaction = JsonSerializer.Deserialize<WalkInTransaction>(operation.Data);
                            if (transaction != null)
                            {
                                await _cloudDb.CreateWalkInTransactionAsync(
                                    transaction.CustomerName,
                                    transaction.ServiceId,
                                    transaction.BarberId,
                                    transaction.TotalAmount,
                                    transaction.CreatedByUserId
                                );
                                return true;
                            }
                        }
                        break;

                    case "Booking":
                        if (operation.OperationType == "Insert")
                        {
                            var booking = JsonSerializer.Deserialize<Booking>(operation.Data);
                            if (booking != null)
                            {
                                await _cloudDb.CreateBookingAsync(
                                    booking.CustomerName,
                                    booking.ServiceId,
                                    booking.BarberId,
                                    booking.TotalAmount,
                                    booking.DepositAmount,
                                    booking.Schedule,
                                    booking.CreatedByUserId
                                );
                                return true;
                            }
                        }
                        else if (operation.OperationType == "Update")
                        {
                            var booking = JsonSerializer.Deserialize<Booking>(operation.Data);
                            if (booking != null)
                            {
                                await _cloudDb.UpdateBookingStatusAsync(booking.BookingId, booking.Status, booking.CreatedByUserId);
                                return true;
                            }
                        }
                        break;

                    case "Barber":
                        if (operation.OperationType == "Insert")
                        {
                            var barber = JsonSerializer.Deserialize<Barber>(operation.Data);
                            if (barber != null)
                            {
                                await _cloudDb.CreateBarberAsync(
                                    barber.FirstName,
                                    barber.LastName,
                                    barber.Email,
                                    barber.Phone,
                                    barber.CommissionRate,
                                    barber.AvailabilityDays,
                                    barber.AvailabilityTimeStart,
                                    barber.AvailabilityTimeEnd
                                );
                                return true;
                            }
                        }
                        else if (operation.OperationType == "Update")
                        {
                            var barber = JsonSerializer.Deserialize<Barber>(operation.Data);
                            if (barber != null)
                            {
                                await _cloudDb.UpdateBarberAvailabilityAsync(
                                    barber.BarberId,
                                    barber.IsActive,
                                    barber.AvailabilityDays,
                                    barber.AvailabilityTimeStart,
                                    barber.AvailabilityTimeEnd,
                                    changedByUserId: null
                                );
                                return true;
                            }
                        }
                        break;
                }

                Debug.WriteLine($"[SyncService] Unknown operation: {operation.OperationType} {operation.EntityType}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SyncService] Error executing operation: {ex.Message}");
                return false;
            }
        }

        public async Task<int> GetPendingCountAsync()
        {
            await EnsureInitializedAsync();
            
            return await _database.Table<PendingOperation>()
                .Where(p => !p.IsSynced)
                .CountAsync();
        }
    }

    [Table("PendingOperation")]
    public class PendingOperation
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string OperationType { get; set; } = string.Empty; // "Insert", "Update", "Delete"

        public string EntityType { get; set; } = string.Empty; // "WalkInTransaction", "Booking", "Barber"

        public string Data { get; set; } = string.Empty; // JSON serialized entity

        public DateTime CreatedAt { get; set; }

        public bool IsSynced { get; set; }

        public DateTime? SyncedAt { get; set; }
    }
}
