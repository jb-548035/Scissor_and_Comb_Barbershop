using hair.Models;
using Microsoft.Maui.Networking;
using System.Diagnostics;

namespace hair.Services
{
    /// <summary>
    /// Hybrid database service that routes operations between local SQL Server and cloud SQL Server
    /// based on network connectivity
    /// </summary>
    public class HybridDatabaseService
    {
        private readonly DatabaseService _localDb;  // Local SQL Server
        private readonly DatabaseService _cloudDb;  // Cloud SQL Server
        private readonly SyncService _syncService;
        private bool IsOnline => Connectivity.NetworkAccess == NetworkAccess.Internet;

        public HybridDatabaseService(DatabaseService localDb, DatabaseService cloudDb, SyncService syncService)
        {
            _localDb = localDb;
            _cloudDb = cloudDb;
            _syncService = syncService;
            Debug.WriteLine("[HybridDB] Initialized with local and cloud databases");
        }

        public async Task<bool> TestConnectionAsync()
        {
            if (IsOnline)
            {
                try
                {
                    var cloudConnected = await _cloudDb.TestConnectionAsync();
                    Debug.WriteLine($"[HybridDB] Cloud connection: {cloudConnected}");
                    return cloudConnected;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HybridDB] Cloud connection failed: {ex.Message}, trying local");
                }
            }
            
            try
            {
                var localConnected = await _localDb.TestConnectionAsync();
                Debug.WriteLine($"[HybridDB] Local connection: {localConnected}");
                return localConnected;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HybridDB] Local connection failed: {ex.Message}");
                return false;
            }
        }

        #region Audit Log Operations
        public async Task<List<AuditLog>> GetAuditLogsAsync()
        {
            try
            {
                var local = await _localDb.GetAuditLogsAsync();
                Debug.WriteLine($"[HybridDB] Loaded {local.Count} audit logs from local");
                return local;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HybridDB] Local audit log fetch failed: {ex.Message}");
            }

            if (IsOnline)
            {
                try
                {
                    var cloud = await _cloudDb.GetAuditLogsAsync();
                    Debug.WriteLine($"[HybridDB] Loaded {cloud.Count} audit logs from cloud");
                    return cloud;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HybridDB] Cloud audit log fetch failed: {ex.Message}");
                }
            }

            return new List<AuditLog>();
        }

        public async Task<bool> LogAuditAsync(string tableName, int recordId, string action, string oldValues, string newValues, int? changedByUserId)
        {
            // Local-first so offline changes are always captured
            bool localOk = false;
            try
            {
                localOk = await _localDb.LogAuditAsync(tableName, recordId, action, oldValues, newValues, changedByUserId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HybridDB] Local audit log insert failed: {ex.Message}");
            }

            if (IsOnline)
            {
                try
                {
                    await _cloudDb.LogAuditAsync(tableName, recordId, action, oldValues, newValues, changedByUserId);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HybridDB] Cloud audit log insert failed: {ex.Message}");
                }
            }

            return localOk;
        }

        public async Task<bool> DeleteAuditLogAsync(int auditLogId, int? changedByUserId = null)
        {
            // Local-first
            bool localOk = false;
            try
            {
                localOk = await _localDb.DeleteAuditLogAsync(auditLogId, changedByUserId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HybridDB] Local audit log delete failed: {ex.Message}");
            }

            if (IsOnline)
            {
                try
                {
                    await _cloudDb.DeleteAuditLogAsync(auditLogId, changedByUserId);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HybridDB] Cloud audit log delete failed: {ex.Message}");
                }
            }

            return localOk;
        }
        #endregion

        #region Service Operations
        public async Task<List<Service>> GetServicesAsync()
        {
            if (IsOnline)
            {
                try
                {
                    var services = await _cloudDb.GetServicesAsync();
                    Debug.WriteLine($"[HybridDB] Loaded {services.Count} services from cloud");
                    return services;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HybridDB] Cloud fetch failed, using local: {ex.Message}");
                }
            }
            
            try
            {
                var services = await _localDb.GetServicesAsync();
                Debug.WriteLine($"[HybridDB] Loaded {services.Count} services from local");
                return services;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HybridDB] Local fetch failed: {ex.Message}");
                return new List<Service>();
            }
        }

        public async Task<int> CreateServiceAsync(string serviceName, decimal price, int? createdByUserId = null)
        {
            if (IsOnline)
            {
                try
                {
                    // Save to cloud first
                    var cloudId = await _cloudDb.CreateServiceAsync(serviceName, price, createdByUserId);
                    Debug.WriteLine($"[HybridDB] Service created in cloud: {cloudId}");

                    // Also save to local to keep them in sync
                    try
                    {
                        await _localDb.CreateServiceAsync(serviceName, price, createdByUserId);
                        Debug.WriteLine($"[HybridDB] Service also saved to local database");
                    }
                    catch (Exception localEx)
                    {
                        Debug.WriteLine($"[HybridDB] Failed to save to local: {localEx.Message}");
                    }

                    return cloudId;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HybridDB] Cloud insert failed: {ex.Message}");
                }
            }

            // Offline: Save to local and queue for sync
            var localId = await _localDb.CreateServiceAsync(serviceName, price, createdByUserId);
            var service = new Service
            {
                ServiceId = localId,
                ServiceName = serviceName,
                Price = price
            };
            await _syncService.QueueOperationAsync("Insert", "Service", service);
            Debug.WriteLine($"[HybridDB] Service saved to local and queued for sync: {localId}");
            return localId;
        }

        public async Task<bool> UpdateServiceAsync(int serviceId, string serviceName, decimal price, int? changedByUserId = null)
        {
            if (IsOnline)
            {
                try
                {
                    // Update cloud first
                    var success = await _cloudDb.UpdateServiceAsync(serviceId, serviceName, price, changedByUserId);

                    // Also update local to keep them in sync
                    try
                    {
                        await _localDb.UpdateServiceAsync(serviceId, serviceName, price, changedByUserId);
                        Debug.WriteLine($"[HybridDB] Service updated in both cloud and local");
                    }
                    catch (Exception localEx)
                    {
                        Debug.WriteLine($"[HybridDB] Failed to update local: {localEx.Message}");
                    }

                    return success;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HybridDB] Cloud update failed: {ex.Message}");
                }
            }

            // Offline: Update local and queue for sync
            await _localDb.UpdateServiceAsync(serviceId, serviceName, price, changedByUserId);
            var service = new Service
            {
                ServiceId = serviceId,
                ServiceName = serviceName,
                Price = price
            };
            await _syncService.QueueOperationAsync("Update", "Service", service);
            Debug.WriteLine($"[HybridDB] Service updated in local and queued for sync");
            return true;
        }

        public async Task<bool> DeleteServiceAsync(int serviceId, int? changedByUserId = null)
        {
            if (IsOnline)
            {
                try
                {
                    // Delete from cloud first
                    var success = await _cloudDb.DeleteServiceAsync(serviceId, changedByUserId);

                    // Also delete from local to keep them in sync
                    try
                    {
                        await _localDb.DeleteServiceAsync(serviceId, changedByUserId);
                        Debug.WriteLine($"[HybridDB] Service deleted from both cloud and local");
                    }
                    catch (Exception localEx)
                    {
                        Debug.WriteLine($"[HybridDB] Failed to delete from local: {localEx.Message}");
                    }

                    return success;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HybridDB] Cloud delete failed: {ex.Message}");
                }
            }

            // Offline: Delete from local and queue for sync
            await _localDb.DeleteServiceAsync(serviceId, changedByUserId);
            var service = new Service { ServiceId = serviceId };
            await _syncService.QueueOperationAsync("Delete", "Service", service);
            Debug.WriteLine($"[HybridDB] Service deleted from local and queued for sync");
            return true;
        }
        #endregion

        #region Barber Operations
        public async Task<List<Barber>> GetBarbersAsync()
        {
            List<Barber> barbers = new();

            if (IsOnline)
            {
                try
                {
                    barbers = await _cloudDb.GetBarbersAsync();
                    Debug.WriteLine($"[HybridDB] Loaded {barbers.Count} barbers from cloud");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HybridDB] Cloud fetch failed, using local: {ex.Message}");
                }
            }
            
            if (barbers.Count == 0)
            {
                try
                {
                    barbers = await _localDb.GetBarbersAsync();
                    Debug.WriteLine($"[HybridDB] Loaded {barbers.Count} barbers from local");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HybridDB] Local fetch failed: {ex.Message}");
                    return new List<Barber>();
                }
            }

            // Ensure formatted time is calculated (optional, safe)
            return barbers.Select(b => { var _ = b.AvailabilityTimeFormatted; return b; }).ToList();
        }


        public async Task<int> CreateBarberAsync(string firstName, string lastName, string email, string phone, decimal commissionRate, string availabilityDays, string availabilityTimeStart, string availabilityTimeEnd)
        {
            Debug.WriteLine($"[HybridDB] 8-parameter CreateBarberAsync called with: availabilityDays={availabilityDays}, timeStart={availabilityTimeStart}, timeEnd={availabilityTimeEnd}");
            
            if (IsOnline)
            {
                try
                {
                    // Save to cloud first
                    var cloudId = await _cloudDb.CreateBarberAsync(firstName, lastName, email, phone, commissionRate, availabilityDays, availabilityTimeStart, availabilityTimeEnd);
                    Debug.WriteLine($"[HybridDB] Barber created in cloud: {cloudId}");

                    // Also save to local to keep them in sync
                    try
                    {
                        await _localDb.CreateBarberAsync(firstName, lastName, email, phone, commissionRate, availabilityDays, availabilityTimeStart, availabilityTimeEnd);
                        Debug.WriteLine($"[HybridDB] Barber also saved to local database");
                    }
                    catch (Exception localEx)
                    {
                        Debug.WriteLine($"[HybridDB] Failed to save to local: {localEx.Message}");
                    }

                    return cloudId;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HybridDB] Cloud insert failed: {ex.Message}");
                }
            }

            // Offline: Save to local and queue for sync
            var localId = await _localDb.CreateBarberAsync(firstName, lastName, email, phone, commissionRate, availabilityDays, availabilityTimeStart, availabilityTimeEnd);
            var barber = new Barber
            {
                BarberId = localId,
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                Phone = phone,
                CommissionRate = commissionRate,
                AvailabilityDays = availabilityDays,
                AvailabilityTimeStart = availabilityTimeStart,
                AvailabilityTimeEnd = availabilityTimeEnd
            };
            await _syncService.QueueOperationAsync("Insert", "Barber", barber);
            Debug.WriteLine($"[HybridDB] Barber saved to local and queued for sync: {localId}");
            return localId;
        }

        public async Task<bool> UpdateBarberAvailabilityAsync(int barberId, bool isActive, string availabilityDays, string availabilityTimeStart, string availabilityTimeEnd, int? changedByUserId = null)
        {
            if (IsOnline)
            {
                try
                {
                    // Update cloud first
                    var success = await _cloudDb.UpdateBarberAvailabilityAsync(barberId, isActive, availabilityDays, availabilityTimeStart, availabilityTimeEnd, changedByUserId);

                    // Also update local to keep them in sync
                    try
                    {
                        await _localDb.UpdateBarberAvailabilityAsync(barberId, isActive, availabilityDays, availabilityTimeStart, availabilityTimeEnd, changedByUserId);
                        Debug.WriteLine($"[HybridDB] Barber availability updated in both cloud and local");
                    }
                    catch (Exception localEx)
                    {
                        Debug.WriteLine($"[HybridDB] Failed to update local availability: {localEx.Message}");
                    }

                    return success;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HybridDB] Cloud update failed: {ex.Message}");
                }
            }

            // Offline: Update local and queue for sync
            await _localDb.UpdateBarberAvailabilityAsync(barberId, isActive, availabilityDays, availabilityTimeStart, availabilityTimeEnd, changedByUserId);
            var barber = new Barber
            {
                BarberId = barberId,
                IsActive = isActive,
                AvailabilityDays = availabilityDays,
                AvailabilityTimeStart = availabilityTimeStart,
                AvailabilityTimeEnd = availabilityTimeEnd
            };
            await _syncService.QueueOperationAsync("Update", "Barber", barber);
            Debug.WriteLine($"[HybridDB] Barber availability updated in local and queued for sync");
            return true;
        }

        public async Task<bool> UpdateBarberStatusAsync(int barberId, bool isActive, int? changedByUserId = null)
        {
            if (IsOnline)
            {
                try
                {
                    // Update cloud first
                    var success = await _cloudDb.UpdateBarberStatusAsync(barberId, isActive, changedByUserId);
                    
                    // Also update local to keep them in sync
                    try
                    {
                        await _localDb.UpdateBarberStatusAsync(barberId, isActive, changedByUserId);
                        Debug.WriteLine($"[HybridDB] Barber status updated in both cloud and local");
                    }
                    catch (Exception localEx)
                    {
                        Debug.WriteLine($"[HybridDB] Failed to update local: {localEx.Message}");
                    }
                    
                    return success;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HybridDB] Cloud update failed: {ex.Message}");
                }
            }

            // Offline: Update local and queue for sync
            await _localDb.UpdateBarberStatusAsync(barberId, isActive, changedByUserId);
            var barber = new Barber { BarberId = barberId, IsActive = isActive };
            await _syncService.QueueOperationAsync("Update", "Barber", barber);
            Debug.WriteLine($"[HybridDB] Barber status updated in local and queued for sync");
            return true;
        }
        #endregion

        #region WalkIn Transaction Operations
        public async Task<List<WalkInTransaction>> GetWalkInTransactionsAsync()
        {
            if (IsOnline)
            {
                try
                {
                    var transactions = await _cloudDb.GetWalkInTransactionsAsync();
                    Debug.WriteLine($"[HybridDB] Loaded {transactions.Count} walk-in transactions from cloud");
                    return transactions;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HybridDB] Cloud fetch failed: {ex.Message}");
                }
            }
            
            try
            {
                var transactions = await _localDb.GetWalkInTransactionsAsync();
                Debug.WriteLine($"[HybridDB] Loaded {transactions.Count} walk-in transactions from local");
                return transactions;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HybridDB] Local fetch failed: {ex.Message}");
                return new List<WalkInTransaction>();
            }
        }

        public async Task<bool> UpdateWalkInStatusAsync(int walkInId, string status, int? changedByUserId = null)
        {
            if (IsOnline)
            {
                try
                {
                    var cloudOk = await _cloudDb.UpdateWalkInStatusAsync(walkInId, status, changedByUserId);

                    try
                    {
                        await _localDb.UpdateWalkInStatusAsync(walkInId, status, changedByUserId);
                    }
                    catch (Exception localEx)
                    {
                        Debug.WriteLine($"[HybridDB] Failed to update local walk-in status: {localEx.Message}");
                    }

                    return cloudOk;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HybridDB] Cloud walk-in status update failed: {ex.Message}");
                }
            }

            return await _localDb.UpdateWalkInStatusAsync(walkInId, status, changedByUserId);
        }

        public async Task<int> CreateWalkInTransactionAsync(string customerName, int serviceId, int barberId, decimal totalAmount, int? createdByUserId = null)
        {
            if (IsOnline)
            {
                try
                {
                    // Save to cloud first
                    var cloudId = await _cloudDb.CreateWalkInTransactionAsync(customerName, serviceId, barberId, totalAmount, createdByUserId);
                    Debug.WriteLine($"[HybridDB] WalkIn transaction created in cloud: {cloudId}");
                    
                    // Also save to local to keep them in sync
                    try
                    {
                        await _localDb.CreateWalkInTransactionAsync(customerName, serviceId, barberId, totalAmount, createdByUserId);
                        Debug.WriteLine($"[HybridDB] WalkIn transaction also saved to local database");
                    }
                    catch (Exception localEx)
                    {
                        Debug.WriteLine($"[HybridDB] Failed to save to local: {localEx.Message}");
                    }
                    
                    return cloudId;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HybridDB] Cloud insert failed: {ex.Message}");
                }
            }

            // Offline: Save to local and queue for sync
            var localId = await _localDb.CreateWalkInTransactionAsync(customerName, serviceId, barberId, totalAmount, createdByUserId);
            var transaction = new WalkInTransaction
            {
                WalkInId = localId,
                CustomerName = customerName,
                ServiceId = serviceId,
                BarberId = barberId,
                TotalAmount = totalAmount,
                CreatedByUserId = createdByUserId
            };
            await _syncService.QueueOperationAsync("Insert", "WalkInTransaction", transaction);
            Debug.WriteLine($"[HybridDB] WalkIn transaction saved to local and queued for sync: {localId}");
            return localId;
        }
        #endregion

        #region Booking Operations
        public async Task<List<Booking>> GetBookingsAsync()
        {
            if (IsOnline)
            {
                try
                {
                    var bookings = await _cloudDb.GetBookingsAsync();
                    Debug.WriteLine($"[HybridDB] Loaded {bookings.Count} bookings from cloud");
                    return bookings;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HybridDB] Cloud fetch failed: {ex.Message}");
                }
            }
            
            try
            {
                var bookings = await _localDb.GetBookingsAsync();
                Debug.WriteLine($"[HybridDB] Loaded {bookings.Count} bookings from local");
                return bookings;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HybridDB] Local fetch failed: {ex.Message}");
                return new List<Booking>();
            }
        }

        public async Task<int> CreateBookingAsync(string customerName, int serviceId, int barberId, decimal totalAmount, decimal depositAmount, DateTime schedule, int? createdByUserId = null)
        {
            if (IsOnline)
            {
                try
                {
                    // Save to cloud first
                    var cloudId = await _cloudDb.CreateBookingAsync(customerName, serviceId, barberId, totalAmount, depositAmount, schedule, createdByUserId);
                    Debug.WriteLine($"[HybridDB] Booking created in cloud: {cloudId}");
                    
                    // Also save to local to keep them in sync
                    try
                    {
                        await _localDb.CreateBookingAsync(customerName, serviceId, barberId, totalAmount, depositAmount, schedule, createdByUserId);
                        Debug.WriteLine($"[HybridDB] Booking also saved to local database");
                    }
                    catch (Exception localEx)
                    {
                        Debug.WriteLine($"[HybridDB] Failed to save to local: {localEx.Message}");
                    }
                    
                    return cloudId;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HybridDB] Cloud insert failed: {ex.Message}");
                }
            }

            // Offline: Save to local and queue for sync
            var localId = await _localDb.CreateBookingAsync(customerName, serviceId, barberId, totalAmount, depositAmount, schedule, createdByUserId);
            var booking = new Booking
            {
                BookingId = localId,
                CustomerName = customerName,
                ServiceId = serviceId,
                BarberId = barberId,
                TotalAmount = totalAmount,
                DepositAmount = depositAmount,
                Schedule = schedule,
                CreatedByUserId = createdByUserId
            };
            await _syncService.QueueOperationAsync("Insert", "Booking", booking);
            Debug.WriteLine($"[HybridDB] Booking saved to local and queued for sync: {localId}");
            return localId;
        }

        public async Task<bool> UpdateBookingStatusAsync(int bookingId, string status, int? changedByUserId = null)
        {
            if (IsOnline)
            {
                try
                {
                    // Update cloud first
                    var success = await _cloudDb.UpdateBookingStatusAsync(bookingId, status, changedByUserId);
                    
                    // Also update local to keep them in sync
                    try
                    {
                        await _localDb.UpdateBookingStatusAsync(bookingId, status, changedByUserId);
                        Debug.WriteLine($"[HybridDB] Booking status updated in both cloud and local");
                    }
                    catch (Exception localEx)
                    {
                        Debug.WriteLine($"[HybridDB] Failed to update local: {localEx.Message}");
                    }
                    
                    return success;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HybridDB] Cloud update failed: {ex.Message}");
                }
            }

            // Offline: Update local and queue for sync
            await _localDb.UpdateBookingStatusAsync(bookingId, status, changedByUserId);
            var booking = new Booking { BookingId = bookingId, Status = status };
            await _syncService.QueueOperationAsync("Update", "Booking", booking);
            Debug.WriteLine($"[HybridDB] Booking status updated in local and queued for sync");
            return true;
        }

        public async Task<bool> UpdateUserActiveStatusAsync(int userId, bool isActive, int? changedByUserId = null)
        {
            if (IsOnline)
            {
                try
                {
                    var cloudOk = await _cloudDb.UpdateUserActiveStatusAsync(userId, isActive, changedByUserId);

                    try
                    {
                        await _localDb.UpdateUserActiveStatusAsync(userId, isActive, changedByUserId);
                    }
                    catch (Exception localEx)
                    {
                        Debug.WriteLine($"[HybridDB] Failed to update local user status: {localEx.Message}");
                    }

                    return cloudOk;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HybridDB] Cloud user status update failed: {ex.Message}");
                }
            }

            return await _localDb.UpdateUserActiveStatusAsync(userId, isActive, changedByUserId);
        }
        #endregion

        #region Sync Operations
        public async Task<(int synced, int failed)> SyncPendingItemsAsync()
        {
            // Delegate to SyncService
            return await _syncService.SyncAllAsync();
        }
        #endregion

        /// <summary>
        /// Delete a barber permanently
        /// </summary>
        public async Task<bool> DeleteBarberAsync(int barberId)
        {
            if (IsOnline)
            {
                try
                {
                    // Delete from cloud first
                    var cloudSuccess = await _cloudDb.DeleteBarberAsync(barberId);
                    Debug.WriteLine($"[HybridDB] Barber deleted in cloud: {cloudSuccess}");

                    // Also delete from local to keep them in sync
                    try
                    {
                        await _localDb.DeleteBarberAsync(barberId);
                        Debug.WriteLine($"[HybridDB] Barber also deleted from local database");
                    }
                    catch (Exception localEx)
                    {
                        Debug.WriteLine($"[HybridDB] Local delete failed: {localEx.Message}");
                        // Continue even if local delete fails
                    }

                    return cloudSuccess;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HybridDB] Cloud delete failed: {ex.Message}");
                }
            }

            // Offline: Delete only from local
            try
            {
                return await _localDb.DeleteBarberAsync(barberId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HybridDB] Local delete failed: {ex.Message}");
                return false;
            }
        }
    }
}
