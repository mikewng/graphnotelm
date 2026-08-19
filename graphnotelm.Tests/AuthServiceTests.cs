using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Services;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Infrastructure.Contracts;
using graphnotelm.Infrastructure.Repository.Contracts;
using graphnotelm.Utils;
using Moq;

namespace graphnotelm.Tests
{
    public class AuthServiceTests
    {
        private readonly Mock<IUserRepository> _usersMock = new();
        private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
        private readonly Mock<IJwtTokenService> _jwtMock = new();

        private readonly AuthService _service;

        public AuthServiceTests()
        {
            _service = new AuthService(_usersMock.Object, _unitOfWorkMock.Object, _jwtMock.Object);
        }

        private static User NewUser(string password)
            => new User
            {
                Id = Guid.NewGuid(),
                Username = "tester",
                Email = "tester@example.com",
                PasswordHash = Cryptography.HashPassword(password)
            };

        // ---------- LoginAsync ----------

        [Theory]
        [InlineData("", "password")]
        [InlineData("   ", "password")]
        [InlineData("user@example.com", "")]
        [InlineData("user@example.com", "   ")]
        public async Task LoginAsync_MissingCredentials_Fails(string email, string password)
        {
            var result = await _service.LoginAsync(new LoginRequest { Email = email, Password = password }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Email and password are required", result.Error);
        }

        [Fact]
        public async Task LoginAsync_UnknownEmail_Fails()
        {
            _usersMock.Setup(u => u.GetByEmailAsync("nobody@example.com", It.IsAny<CancellationToken>()))
                .ReturnsAsync((User?)null);

            var result = await _service.LoginAsync(
                new LoginRequest { Email = "nobody@example.com", Password = "secret123" }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Invalid email or password", result.Error);
        }

        [Fact]
        public async Task LoginAsync_WrongPassword_Fails()
        {
            var user = NewUser("correct-password");
            _usersMock.Setup(u => u.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            var result = await _service.LoginAsync(
                new LoginRequest { Email = user.Email, Password = "wrong-password" }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Invalid email or password", result.Error);
        }

        [Fact]
        public async Task LoginAsync_UpdateFails_Fails()
        {
            var user = NewUser("secret123");
            _usersMock.Setup(u => u.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _usersMock.Setup(u => u.UpdateAsync(user, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var result = await _service.LoginAsync(
                new LoginRequest { Email = user.Email, Password = "secret123" }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Failed to update user login time", result.Error);
        }

        [Fact]
        public async Task LoginAsync_ValidCredentials_ReturnsTokenAndUpdatesLastLogin()
        {
            var user = NewUser("secret123");
            var expiry = DateTime.UtcNow.AddMinutes(15);
            _usersMock.Setup(u => u.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _usersMock.Setup(u => u.UpdateAsync(user, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _jwtMock.Setup(j => j.CreateAccessToken(user)).Returns(("the-token", expiry));

            var result = await _service.LoginAsync(
                new LoginRequest { Email = user.Email, Password = "secret123" }, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("the-token", result.Value!.AccessToken);
            Assert.Equal(expiry, result.Value.ExpiresAtUtc);
            Assert.NotNull(user.LastLoginAt);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        // ---------- RegisterAsync ----------

        [Theory]
        [InlineData("", "user@example.com", "secret123")]
        [InlineData("tester", "", "secret123")]
        [InlineData("tester", "user@example.com", "")]
        public async Task RegisterAsync_MissingFields_Fails(string username, string email, string password)
        {
            var result = await _service.RegisterAsync(
                new RegisterRequest { Username = username, Email = email, Password = password }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Username, email, and password are required", result.Error);
        }

        [Fact]
        public async Task RegisterAsync_ShortPassword_Fails()
        {
            var result = await _service.RegisterAsync(
                new RegisterRequest { Username = "tester", Email = "t@e.com", Password = "12345" }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Password must be at least 6 characters long", result.Error);
        }

        [Fact]
        public async Task RegisterAsync_DuplicateEmail_Fails()
        {
            _usersMock.Setup(u => u.EmailExistsAsync("t@e.com", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var result = await _service.RegisterAsync(
                new RegisterRequest { Username = "tester", Email = "t@e.com", Password = "secret123" }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Email already exists", result.Error);
        }

        [Fact]
        public async Task RegisterAsync_DuplicateUsername_Fails()
        {
            _usersMock.Setup(u => u.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _usersMock.Setup(u => u.UsernameExistsAsync("tester", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var result = await _service.RegisterAsync(
                new RegisterRequest { Username = "tester", Email = "t@e.com", Password = "secret123" }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Username already exists", result.Error);
        }

        [Fact]
        public async Task RegisterAsync_Valid_AddsUserWithVerifiablePasswordHash()
        {
            User? added = null;
            _usersMock.Setup(u => u.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _usersMock.Setup(u => u.UsernameExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _usersMock.Setup(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                .Callback<User, CancellationToken>((u, _) => added = u)
                .Returns(Task.CompletedTask);

            var result = await _service.RegisterAsync(
                new RegisterRequest { Username = "tester", Email = "t@e.com", Password = "secret123" }, CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(added);
            Assert.Equal("tester", added!.Username);
            Assert.Equal("t@e.com", added.Email);
            Assert.NotEqual("secret123", added.PasswordHash);
            Assert.True(Cryptography.VerifyPassword("secret123", added.PasswordHash));
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
