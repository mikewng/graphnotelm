using graphnotelm.Core.Models;

namespace graphnotelm.Core.Services.Contracts
{
    public interface IJwtTokenService
    {
        (string token, DateTime expiresAtUtc) CreateAccessToken(User user);
    }
}
