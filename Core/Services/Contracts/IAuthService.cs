using graphnotelm.Core.Models.DTOs;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services.Contracts
{
    public interface IAuthService
    {
        public Task<Result<AuthResponse>> LoginAsync(LoginRequest loginRequest, CancellationToken cancellationToken);
        public Task<Result> RegisterAsync(RegisterRequest registerRequest, CancellationToken cancellationToken);

        public Task<Result<AccountResponse>> GetAccountAsync(Guid userId, CancellationToken cancellationToken);

        public Task<Result<AccountUpdateResponse>> UpdateAccountAsync(Guid userId, UpdateAccountRequest request, CancellationToken cancellationToken);

        public Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken);

        /// <summary>Re-issues the caller's token with either the configured lifetime or a non-expiring one.</summary>
        public Task<Result<AuthResponse>> SetSessionPreferenceAsync(Guid userId, bool neverExpire, CancellationToken cancellationToken);
    }
}
