using graphnotelm.Core.Models;

namespace graphnotelm.Core.Services.Contracts
{
    public interface IJwtTokenService
    {
        /// <param name="lifetime">Overrides the configured Jwt:AccessTokenMinutes when supplied.</param>
        (string token, DateTime expiresAtUtc) CreateAccessToken(User user, TimeSpan? lifetime = null);
    }
}
