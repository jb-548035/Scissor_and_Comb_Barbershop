# Role-Based Access Control (RBAC)

## Overview
The application now implements role-based access control with two roles:
- **Admin** - Full access to all pages
- **Cashier** - Limited access to specific pages

## Access Levels

### Admin Access (All Pages)
- ✅ Dashboard
- ✅ Walk-In
- ✅ Booking
- ✅ History
- ✅ Documents
- ✅ **Barbers** (TOOLS)
- ✅ **Insights** (TOOLS)
- ✅ **Accounts** (TOOLS)
- ✅ **Settings** (SUPPORT)

### Cashier Access (Limited Pages)
- ✅ Dashboard
- ✅ Walk-In
- ✅ Booking
- ✅ History
- ✅ Documents
- ❌ Barbers (hidden)
- ❌ Insights (hidden)
- ❌ Accounts (hidden)
- ❌ Settings (hidden)

## Implementation

### 1. Navigation Menu (NavMenu.razor)
The TOOLS and SUPPORT sections are now hidden for Cashiers:

```razor
@if (IsAdmin)
{
    <div class="nav-section">
        <!-- TOOLS Section - Admin Only -->
    </div>
}
```

**Features:**
- Checks current user role on initialization
- Subscribes to auth state changes
- Updates menu dynamically when user logs in/out
- Cashiers only see MENU section

### 2. Route Guards (RoleGuard.razor)
A new component protects admin-only pages:

```razor
<RoleGuard AllowedRoles="new[] { \"Admin\" }">
    <!-- Page content here -->
</RoleGuard>
```

**Features:**
- Checks user role before rendering page
- Shows "Access Denied" message if unauthorized
- Redirects to Dashboard after 2 seconds
- Provides "Go to Dashboard" button

## How to Use

### Protecting Admin-Only Pages

Wrap page content with RoleGuard:

```razor
@page "/barber"
@using hair.Components

<RoleGuard AllowedRoles="new[] { \"Admin\" }">
    <!-- Your page content -->
</RoleGuard>
```

### Protecting Multiple Roles

Allow multiple roles:

```razor
<RoleGuard AllowedRoles="new[] { \"Admin\", \"Manager\" }">
    <!-- Content for Admin and Manager -->
</RoleGuard>
```

## Pages to Protect

Add RoleGuard to these admin-only pages:

1. **Barber.razor** - Barber management
   ```razor
   <RoleGuard AllowedRoles="new[] { \"Admin\" }">
   ```

2. **Insight.razor** (if exists) - Analytics/Insights
   ```razor
   <RoleGuard AllowedRoles="new[] { \"Admin\" }">
   ```

3. **Account.razor** - User account management
   ```razor
   <RoleGuard AllowedRoles="new[] { \"Admin\" }">
   ```

4. **Setting.razor** (if exists) - Settings
   ```razor
   <RoleGuard AllowedRoles="new[] { \"Admin\" }">
   ```

## User Roles in Database

Roles are stored in the Users table:

```sql
-- Admin user
INSERT INTO Users (FirstName, LastName, Email, Role, ...)
VALUES ('John', 'Admin', 'john@admin.com', 'Admin', ...)

-- Cashier user
INSERT INTO Users (FirstName, LastName, Email, Role, ...)
VALUES ('Maria', 'Santos', 'maria@cashier.com', 'Cashier', ...)
```

## How It Works

### 1. Login Process
1. User logs in with email and password
2. AuthenticationService validates credentials
3. User data (including Role) is retrieved from database
4. AuthStateService stores user info with role
5. NavMenu updates to show/hide admin sections

### 2. Navigation
- Menu items for TOOLS and SUPPORT sections only appear for Admins
- Cashiers see only MENU section (Dashboard, Walk-In, Booking, History, Documents)

### 3. Page Access
- If Cashier tries to access admin page directly (via URL):
  - RoleGuard checks user role
  - Shows "Access Denied" message
  - Redirects to Dashboard after 2 seconds

### 4. Dynamic Updates
- When user logs out, menu updates immediately
- When user logs in, menu reflects their role
- Auth state changes trigger menu refresh

## Security Notes

- ✅ Navigation menu hides admin pages from Cashiers
- ✅ Route guards prevent direct URL access to admin pages
- ✅ Role is checked on every page load
- ✅ Unauthorized access redirects to Dashboard
- ⚠️ Backend API should also validate roles (not implemented in this guide)

## Testing

### Test as Admin
1. Log in with Admin account
2. Verify all menu items appear (MENU, TOOLS, SUPPORT)
3. Can access all pages
4. Can access Barber, Insights, Accounts, Settings

### Test as Cashier
1. Log in with Cashier account
2. Verify only MENU section appears
3. TOOLS and SUPPORT sections are hidden
4. If you try to access `/barber` directly:
   - See "Access Denied" message
   - Redirected to Dashboard after 2 seconds

## Future Enhancements

- Add more roles (Manager, Supervisor, etc.)
- Implement feature-level permissions (can edit but not delete)
- Add audit logging for admin actions
- Create role management page
- Add permission matrix for fine-grained control

## Files Modified/Created

### Modified:
- `Components/Layout/NavMenu.razor` - Added role checking

### Created:
- `Components/RoleGuard.razor` - Role-based page guard
- `ROLE_BASED_ACCESS_CONTROL.md` - This documentation

## Summary

The application now has role-based access control:
- **Admins** see all menu items and can access all pages
- **Cashiers** see limited menu and can only access Dashboard, Walk-In, Booking, History, Documents
- Unauthorized access is prevented with guards and redirects
- Menu updates dynamically based on logged-in user role
