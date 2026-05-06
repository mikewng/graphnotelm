using graphnotelm.Core.Models.DTOs;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services.Contracts
{
    public interface IAuthService
    {
        public Task<Result<AuthResponse>> LoginAsync(LoginRequest loginRequest, CancellationToken cancellationToken);
        public Task<Result> RegisterAsync(RegisterRequest registerRequest, CancellationToken cancellationToken);
    }
}
