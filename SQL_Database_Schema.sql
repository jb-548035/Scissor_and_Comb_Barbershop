CREATE TABLE Users (
    UserId INT PRIMARY KEY IDENTITY(1,1),
    FirstName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    Email NVARCHAR(100) NOT NULL UNIQUE,
    Phone NVARCHAR(20) NULL,
    Role NVARCHAR(50) NOT NULL, 
    PasswordHash NVARCHAR(MAX) NOT NULL,
    CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
    IsActive BIT NOT NULL DEFAULT 1
);

CREATE TABLE Services (
    ServiceId INT PRIMARY KEY IDENTITY(1,1),
    ServiceName NVARCHAR(100) NOT NULL UNIQUE,
    Price DECIMAL(10,2) NOT NULL,
    CreatedDate DATETIME NOT NULL DEFAULT GETDATE()
);

CREATE TABLE Barbers (
    BarberId INT PRIMARY KEY IDENTITY(1,1),
    FirstName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    Email NVARCHAR(100) NOT NULL UNIQUE,
    Phone NVARCHAR(20) NULL,
    CommissionRate DECIMAL(5,2) NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedDate DATETIME NOT NULL DEFAULT GETDATE()
);

CREATE TABLE WalkInTransactions (
    WalkInId INT PRIMARY KEY IDENTITY(1,1),
    CustomerName NVARCHAR(100) NOT NULL,
    ServiceId INT NOT NULL,
    BarberId INT NOT NULL,
    TotalAmount DECIMAL(10,2) NOT NULL,
    DateTime DATETIME NOT NULL DEFAULT GETDATE(),
    Status NVARCHAR(50) NOT NULL DEFAULT 'Paid',
    CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),

    FOREIGN KEY (ServiceId) REFERENCES Services(ServiceId),
    FOREIGN KEY (BarberId) REFERENCES Barbers(BarberId)
);

CREATE TABLE Bookings (
    BookingId INT PRIMARY KEY IDENTITY(1,1),
    CustomerName NVARCHAR(100) NOT NULL,
    ServiceId INT NOT NULL,
    BarberId INT NOT NULL,
    TotalAmount DECIMAL(10,2) NOT NULL,
    DepositAmount DECIMAL(10,2) NOT NULL DEFAULT 0,
    Schedule DATETIME NOT NULL,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Pending',
    CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),

    FOREIGN KEY (ServiceId) REFERENCES Services(ServiceId),
    FOREIGN KEY (BarberId) REFERENCES Barbers(BarberId)
);


-------------------Changes-------------------------
CREATE TABLE AuditLogs (
    AuditLogId INT PRIMARY KEY IDENTITY(1,1),
    TableName NVARCHAR(100) NOT NULL,
    RecordId INT NOT NULL,
    Action NVARCHAR(20) NOT NULL, -- INSERT, UPDATE, DELETE
    OldValues NVARCHAR(MAX) NULL,
    NewValues NVARCHAR(MAX) NULL,
    ChangedByUserId INT NULL,
    ChangedDate DATETIME NOT NULL DEFAULT GETDATE()
);

ALTER TABLE WalkInTransactions
ADD CreatedByUserId INT NULL;

ALTER TABLE Bookings
ADD CreatedByUserId INT NULL;

----------
ALTER TABLE WalkInTransactions
ADD CONSTRAINT FK_WalkIn_User
FOREIGN KEY (CreatedByUserId) REFERENCES Users(UserId);

ALTER TABLE Bookings
ADD CONSTRAINT FK_Booking_User
FOREIGN KEY (CreatedByUserId) REFERENCES Users(UserId);

ALTER TABLE Users
ADD RequiresPasswordChange BIT NOT NULL DEFAULT 1;

-----------
CREATE TRIGGER trg_WalkIn_Audit
ON WalkInTransactions
AFTER UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO AuditLogs (
        TableName,
        RecordId,
        Action,
        OldValues,
        NewValues,
        ChangedByUserId
    )
    SELECT
        'WalkInTransactions',
        d.WalkInId,
        CASE 
            WHEN i.WalkInId IS NULL THEN 'DELETE'
            ELSE 'UPDATE'
        END,
        (SELECT d.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        (SELECT i.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        i.CreatedByUserId
    FROM deleted d
    LEFT JOIN inserted i ON d.WalkInId = i.WalkInId;
END;

-----------
CREATE TABLE Roles (
    RoleId INT PRIMARY KEY IDENTITY(1,1),
    RoleName NVARCHAR(50) UNIQUE NOT NULL
);

------------
EXEC sp_rename 
'WalkInTransactions.DateTime',
'ServiceDate',
'COLUMN';

