using System;
using System.Threading.Tasks;

namespace hair.Services
{
    /// <summary>
    /// Service to manage authentication state across the application
    /// </summary>
    public class AuthStateService
    {
        private UserInfo _currentUser;
        public event Action OnAuthStateChanged;

        /// <summary>
        /// Get current logged-in user
        /// </summary>
        public UserInfo GetCurrentUser()
        {
            return _currentUser;
        }

        /// <summary>
        /// Check if user is logged in
        /// </summary>
        public bool IsLoggedIn()
        {
            return _currentUser != null && _currentUser.IsLoggedIn;
        }

        /// <summary>
        /// Set current user (called after successful login)
        /// </summary>
        public void SetCurrentUser(UserInfo user)
        {
            _currentUser = user;
            NotifyAuthStateChanged();
        }

        /// <summary>
        /// Clear current user (called on logout)
        /// </summary>
        public void Logout()
        {
            _currentUser = null;
            NotifyAuthStateChanged();
        }

        /// <summary>
        /// Notify all subscribers that auth state has changed
        /// </summary>
        private void NotifyAuthStateChanged()
        {
            OnAuthStateChanged?.Invoke();
        }
    }
}
