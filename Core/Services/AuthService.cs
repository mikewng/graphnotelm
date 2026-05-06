using graphnotelm.Infrastructure.Contracts;
using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Infrastructure.Repository.Contracts;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services
{
    public class AuthService : IAuthService
    {
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

            var (token, expiresAtUtc) = _jwt.CreateAccessToken(user);

            return Result<AuthResponse>.Ok(new AuthResponse
            {
                AccessToken = token,
                ExpiresAtUtc = expiresAtUtc
            });
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
            var passwordHash = Cryptography.HashPassword(registerRequest.Password);
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = registerRequest.Username,
                Email = registerRequest.Email,
                PasswordHash = passwordHash,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };

            await _users.AddAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Ok();
        }


    }
}
