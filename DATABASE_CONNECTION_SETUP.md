# Database Connection Setup Guide

## Overview
This guide explains how to connect your Hair Salon Management System to SQL Server Management Studio (SSMS 21) and configure the walk-in customer functionality.

## Prerequisites
- SQL Server 2019 or later installed
- SSMS 21 installed
- Database `db_hair` created with tables from `SQL_Database_Schema.sql`

## Step 1: Verify Database Setup

1. Open SSMS 21
2. Connect to your SQL Server instance
3. Verify that the `db_hair` database exists
4. Verify all tables are created:
   - Customers
   - Services
   - WalkInTransactions
   - Users
   - Roles
   - Accounts
   - Bookings
   - Transactions

## Step 2: Insert Sample Data

Before testing, insert some sample services into the database:

```sql
USE db_hair;

-- Insert sample services
INSERT INTO Services (ServiceName, BasePrice, Duration, Description, IsActive)
VALUES 
    ('Haircut', 120.00, 30, 'Basic haircut service', 1),
    ('Haircut + Dye', 150.00, 60, 'Haircut with hair dye service', 1),
    ('Hair Wash', 50.00, 15, 'Hair washing service', 1);

-- Insert sample user (cashier)
INSERT INTO Roles (RoleName, Description, IsActive)
VALUES ('Cashier', 'Cashier role for transactions', 1);

INSERT INTO Users (FirstName, LastName, Email, Phone, RoleId, PasswordHash, IsActive)
VALUES ('John', 'Doe', 'cashier@salon.com', '09123456789', 1, 'hash_placeholder', 1);
```

## Step 3: Configure Connection in Customer.razor

Open `d:\IT12\hair\hair\Components\Pages\Customer.razor` and find the `OnInitializedAsync()` method.

Update the database connection parameters:

```csharp
_dbService = new DatabaseService(
    serverName: "YOUR_SERVER_NAME",  // e.g., "localhost", "DESKTOP-ABC123", or "192.168.1.100"
    databaseName: "db_hair",
    userId: "",                       // Leave empty for Windows Authentication
    password: ""                      // Leave empty for Windows Authentication
);
```

### Finding Your Server Name

**Option 1: Windows Authentication (Recommended)**
- Leave `userId` and `password` empty
- Use your computer name or "localhost"
- Example: `serverName: "DESKTOP-ABC123"`

**Option 2: SQL Server Authentication**
- Provide SQL login credentials
- Example:
  ```csharp
  serverName: "localhost",
  userId: "sa",
  password: "YourPassword123"
  ```

### How to Find Your Server Name

1. Open SSMS 21
2. In the "Connect to Server" dialog, look at the server name field
3. It typically looks like:
   - `DESKTOP-ABC123` (local machine)
   - `DESKTOP-ABC123\SQLEXPRESS` (SQL Express instance)
   - `192.168.1.100` (network machine)
   - `SERVER-NAME\INSTANCE` (named instance)

## Step 4: Update Current User ID

In the same `Customer.razor` file, update the `CurrentUserId` to match your cashier user:

```csharp
private int CurrentUserId = 1; // Change to your actual user ID from the Users table
```

To find the correct ID:
```sql
SELECT UserId, FirstName, LastName, Email FROM Users;
```

## Step 5: Build and Test

1. Open the project in Visual Studio
2. Build the solution (Ctrl+Shift+B)
3. Run the application (F5)
4. Navigate to the Customer page
5. Test the walk-in customer functionality:
   - Click "Add Walk-in Customer"
   - Enter customer name
   - Select a service
   - Click "Save Customer"
   - Verify the transaction appears in the table

## Troubleshooting

### Connection Failed Error
- Check that SQL Server is running
- Verify the server name is correct
- For Windows Authentication, ensure your Windows account has access to SQL Server
- Check firewall settings if connecting to a remote server

### No Services Found
- Verify you inserted sample services into the Services table
- Run: `SELECT * FROM Services;` in SSMS to check

### Transaction Not Saving
- Check the error message in the UI
- Verify the UserId exists in the Users table
- Check SQL Server logs for detailed error information

### Database Connection String Issues

**Windows Authentication:**
```
Server=DESKTOP-ABC123;Database=db_hair;Integrated Security=true;
```

**SQL Authentication:**
```
Server=DESKTOP-ABC123;Database=db_hair;User Id=sa;Password=YourPassword;
```

## Features Implemented

### Walk-in Customer Transaction
- Create new walk-in customers with optional phone number
- Automatically link to existing customers by phone
- Record transactions with service selection
- Display all walk-in transactions in a table
- Real-time database synchronization

### Database Operations
- `GetServicesAsync()` - Retrieve available services
- `GetCustomerByPhoneAsync()` - Check if customer exists
- `CreateCustomerAsync()` - Create new customer
- `CreateWalkInTransactionAsync()` - Record transaction
- `GetWalkInTransactionsAsync()` - Retrieve all transactions
- `UpdateCustomerLastVisitAsync()` - Update last visit date

## Next Steps

1. Implement authentication to automatically set `CurrentUserId`
2. Add payment method selection (Cash, Card, Online)
3. Add notes/comments field for transactions
4. Implement transaction history filtering
5. Add customer lookup and history view
6. Implement receipt printing

## Support

For issues or questions, check the error messages displayed in the UI or review the debug output in Visual Studio.
