using graphnotelm.Infrastructure.Contracts;
using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Models.Mappers;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Infrastructure.Repository.Contracts;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services
{
    public class AuthService : IAuthService
    {
        // JWTs must carry an expiry, so "never expires" is a century-long one.
        private static readonly TimeSpan NonExpiringSessionLifetime = TimeSpan.FromDays(36500);

        private readonly IUserRepository _users;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IJwtTokenService _jwt;

        public AuthService(IUserRepository userRepository, IUnitOfWork unitOfWork, IJwtTokenService jwt)
        {
            _users = userRepository;
            _unitOfWork = unitOfWork;
            _jwt = jwt;
        }

        public async Task<Result<AuthResponse>> LoginAsync(LoginRequest loginRequest, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(loginRequest.Email) || string.IsNullOrWhiteSpace(loginRequest.Password))
                return Result<AuthResponse>.Fail("Email and password are required");

            var user = await _users.GetByEmailAsync(loginRequest.Email, cancellationToken);
            if (user == null || !Cryptography.VerifyPassword(loginRequest.Password, user.PasswordHash))
                return Result<AuthResponse>.Fail("Invalid email or password");

            user.LastLoginAt = DateTime.UtcNow;

            var updateSuccess = await _users.UpdateAsync(user, cancellationToken);
            if (!updateSuccess)
                return Result<AuthResponse>.Fail("Failed to update user login time");

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<AuthResponse>.Ok(_jwt.CreateAccessToken(user).ToAuthResponse());
        }

        public async Task<Result> RegisterAsync(RegisterRequest registerRequest, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(registerRequest.Username) ||
                string.IsNullOrWhiteSpace(registerRequest.Email) ||
                string.IsNullOrWhiteSpace(registerRequest.Password))
            {
                return Result.Fail("Username, email, and password are required");
            }

            if (registerRequest.Password.Length < 6)
            {
                return Result.Fail("Password must be at least 6 characters long");
            }

            if (await _users.EmailExistsAsync(registerRequest.Email, cancellationToken))
            {
                return Result.Fail("Email already exists");
            }

            if (await _users.UsernameExistsAsync(registerRequest.Username, cancellationToken))
            {
                return Result.Fail("Username already exists");
            }

            // Create Password Hash and New User Object
            var user = registerRequest.ToUser(Cryptography.HashPassword(registerRequest.Password));
            user.LastLoginAt = DateTime.UtcNow;

            await _users.AddAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Ok();
        }

        public async Task<Result<AccountResponse>> GetAccountAsync(Guid userId, CancellationToken cancellationToken)
        {
            var user = await _users.GetByIdAsync(userId, cancellationToken);
            if (user == null)
                return Result<AccountResponse>.Fail("Account not found");

            return Result<AccountResponse>.Ok(user.ToAccountResponse());
        }

        public async Task<Result<AccountUpdateResponse>> UpdateAccountAsync(Guid userId, UpdateAccountRequest request, CancellationToken cancellationToken)
        {
            var user = await _users.GetByIdAsync(userId, cancellationToken);
            if (user == null)
                return Result<AccountUpdateResponse>.Fail("Account not found");

            var username = request.Username?.Trim();
            var email = request.Email?.Trim();

            if (!string.IsNullOrEmpty(username) && !username.Equals(user.Username, StringComparison.Ordinal))
            {
                if (await _users.UsernameExistsAsync(username, cancellationToken))
                    return Result<AccountUpdateResponse>.Fail("Username already exists");

                user.Username = username;
            }

            if (!string.IsNullOrEmpty(email) && !email.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
            {
                if (await _users.EmailExistsAsync(email, cancellationToken))
                    return Result<AccountUpdateResponse>.Fail("Email already exists");

                user.Email = email;
            }

            if (!await _users.UpdateAsync(user, cancellationToken))
                return Result<AccountUpdateResponse>.Fail("Failed to update account");

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Email and username are baked into the JWT claims, so hand back a fresh token.
            return Result<AccountUpdateResponse>.Ok(new AccountUpdateResponse
            {
                Account = user.ToAccountResponse(),
                Session = _jwt.CreateAccessToken(user).ToAuthResponse()
            });
        }

        public async Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
                return Result.Fail("Current and new password are required");

            if (request.NewPassword.Length < 6)
                return Result.Fail("Password must be at least 6 characters long");

            var user = await _users.GetByIdAsync(userId, cancellationToken);
            if (user == null)
                return Result.Fail("Account not found");

            if (!Cryptography.VerifyPassword(request.CurrentPassword, user.PasswordHash))
                return Result.Fail("Current password is incorrect");

            user.PasswordHash = Cryptography.HashPassword(request.NewPassword);

            if (!await _users.UpdateAsync(user, cancellationToken))
                return Result.Fail("Failed to update password");

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Ok();
        }

        public async Task<Result<AuthResponse>> SetSessionPreferenceAsync(Guid userId, bool neverExpire, CancellationToken cancellationToken)
        {
            var user = await _users.GetByIdAsync(userId, cancellationToken);
            if (user == null)
                return Result<AuthResponse>.Fail("Account not found");

            var lifetime = neverExpire ? NonExpiringSessionLifetime : (TimeSpan?)null;

            return Result<AuthResponse>.Ok(_jwt.CreateAccessToken(user, lifetime).ToAuthResponse());
        }
    }
}
