namespace graphnotelm.Core.Models.DTOs
{
    public class AuthResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
    }

    public class AccountResponse
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }

    /// <summary>
    /// Returned when a change invalidates the caller's token (email/username live in
    /// the JWT claims), so the client can swap it in without a re-login.
    /// </summary>
    public class AccountUpdateResponse
    {
        public AccountResponse Account { get; set; } = new();
        public AuthResponse Session { get; set; } = new();
    }
}
