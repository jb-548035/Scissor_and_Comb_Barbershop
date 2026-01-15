-- Clear all users from the database
-- This will allow the system to recreate the default Admin on next run

DELETE FROM Users;

-- Reset identity seed for Users table
DBCC CHECKIDENT ('Users', RESEED, 0);

-- Verify users table is empty
SELECT COUNT(*) as UserCount FROM Users;
