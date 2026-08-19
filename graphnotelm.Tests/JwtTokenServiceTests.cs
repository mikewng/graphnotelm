using graphnotelm.Core.Models;
using graphnotelm.Core.Services;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;

namespace graphnotelm.Tests
{
    public class JwtTokenServiceTests
    {
        private const string SigningKey = "a-very-long-test-signing-key-with-plenty-of-entropy-0123456789";

        private static IConfiguration BuildConfig(
            string? issuer = "test-issuer",
            string? audience = "test-audience",
            string? key = SigningKey,
            string? accessTokenMinutes = "30")
        {
            var values = new Dictionary<string, string?>();
            if (issuer != null) values["Jwt:Issuer"] = issuer;
            if (audience != null) values["Jwt:Audience"] = audience;
            if (key != null) values["Jwt:Key"] = key;
            if (accessTokenMinutes != null) values["Jwt:AccessTokenMinutes"] = accessTokenMinutes;

            return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        }

        private static User NewUser() => new User
        {
            Id = Guid.NewGuid(),
            Username = "tester",
            Email = "tester@example.com"
        };

        [Fact]
        public void CreateAccessToken_ReturnsParsableJwtWithExpectedClaims()
        {
            var service = new JwtTokenService(BuildConfig());
            var user = NewUser();

            var (token, _) = service.CreateAccessToken(user);

            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            Assert.Equal("test-issuer", jwt.Issuer);
            Assert.Contains("test-audience", jwt.Audiences);
            Assert.Equal(user.Id.ToString(), jwt.Claims.First(c => c.Type == "sub").Value);
            Assert.Equal(user.Email, jwt.Claims.First(c => c.Type == "email").Value);
            Assert.Equal(user.Username, jwt.Claims.First(c => c.Type == "username").Value);
            Assert.NotEmpty(jwt.Claims.First(c => c.Type == "jti").Value);
        }

        [Fact]
        public void CreateAccessToken_UsesConfiguredLifetime()
        {
            var service = new JwtTokenService(BuildConfig(accessTokenMinutes: "30"));

            var before = DateTime.UtcNow;
            var (_, expiresAtUtc) = service.CreateAccessToken(NewUser());
            var after = DateTime.UtcNow;

            Assert.InRange(expiresAtUtc, before.AddMinutes(30), after.AddMinutes(30));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("not-a-number")]
        public void CreateAccessToken_InvalidOrMissingLifetime_DefaultsTo15Minutes(string? minutes)
        {
            var service = new JwtTokenService(BuildConfig(accessTokenMinutes: minutes));

            var before = DateTime.UtcNow;
            var (_, expiresAtUtc) = service.CreateAccessToken(NewUser());
            var after = DateTime.UtcNow;

            Assert.InRange(expiresAtUtc, before.AddMinutes(15), after.AddMinutes(15));
        }

        [Fact]
        public void CreateAccessToken_MissingIssuer_Throws()
        {
            var service = new JwtTokenService(BuildConfig(issuer: null));
            var ex = Assert.Throws<InvalidOperationException>(() => service.CreateAccessToken(NewUser()));
            Assert.Contains("Issuer", ex.Message);
        }

        [Fact]
        public void CreateAccessToken_MissingAudience_Throws()
        {
            var service = new JwtTokenService(BuildConfig(audience: null));
            var ex = Assert.Throws<InvalidOperationException>(() => service.CreateAccessToken(NewUser()));
            Assert.Contains("Audience", ex.Message);
        }

        [Fact]
        public void CreateAccessToken_MissingKey_Throws()
        {
            var service = new JwtTokenService(BuildConfig(key: null));
            var ex = Assert.Throws<InvalidOperationException>(() => service.CreateAccessToken(NewUser()));
            Assert.Contains("Key", ex.Message);
        }

        [Fact]
        public void CreateAccessToken_GeneratesUniqueTokensPerCall()
        {
            var service = new JwtTokenService(BuildConfig());
            var user = NewUser();

            var (first, _) = service.CreateAccessToken(user);
            var (second, _) = service.CreateAccessToken(user);

            Assert.NotEqual(first, second); // jti claim is a fresh GUID each time
        }
    }
}
