using graphnotelm.Core.Models.DTOs;
using Riok.Mapperly.Abstractions;

namespace graphnotelm.Core.Models.Mappers
{
    [Mapper]
    public static partial class UserMapper
    {
        // PasswordHash comes from the already-hashed parameter; the raw password is never mapped.
        [MapperIgnoreSource(nameof(RegisterRequest.Password))]
        [MapperIgnoreTarget(nameof(User.Id))]
        [MapperIgnoreTarget(nameof(User.CreatedAt))]
        [MapperIgnoreTarget(nameof(User.LastLoginAt))]
        public static partial User ToUser(this RegisterRequest request, string passwordHash);

        public static AuthResponse ToAuthResponse(this (string Token, DateTime ExpiresAtUtc) accessToken)
            => new() { AccessToken = accessToken.Token, ExpiresAtUtc = accessToken.ExpiresAtUtc };

        public static AccountResponse ToAccountResponse(this User user)
            => new()
            {
                Id          = user.Id,
                Username    = user.Username,
                Email       = user.Email,
                CreatedAt   = user.CreatedAt,
                LastLoginAt = user.LastLoginAt
            };
    }
}
