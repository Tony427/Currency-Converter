using CurrencyConverter.Application.Services;
using CurrencyConverter.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace CurrencyConverter.Tests.Services
{
    public class JwtTokenServiceTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<ILogger<JwtTokenService>> _mockLogger;
        private readonly JwtTokenService _service;
        private readonly Dictionary<string, string> _jwtSettings;

        public JwtTokenServiceTests()
        {
            _jwtSettings = new Dictionary<string, string>
            {
                ["JWT:Secret"] = "ThisIsAVeryLongSecretKeyForTestingPurposesWithAtLeast256Bits",
                ["JWT:Issuer"] = "TestIssuer",
                ["JWT:Audience"] = "TestAudience"
            };

            _mockConfiguration = new Mock<IConfiguration>();
            _mockConfiguration.Setup(x => x["JWT:Secret"]).Returns(_jwtSettings["JWT:Secret"]);
            _mockConfiguration.Setup(x => x["JWT:Issuer"]).Returns(_jwtSettings["JWT:Issuer"]);
            _mockConfiguration.Setup(x => x["JWT:Audience"]).Returns(_jwtSettings["JWT:Audience"]);

            var userStore = new Mock<IUserStore<ApplicationUser>>();
            _mockUserManager = new Mock<UserManager<ApplicationUser>>(
                userStore.Object, null, null, null, null, null, null, null, null);

            _mockLogger = new Mock<ILogger<JwtTokenService>>();

            _service = new JwtTokenService(
                _mockConfiguration.Object,
                _mockUserManager.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task GenerateTokenAsync_ValidUser_ReturnsValidJwtToken()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = "test@example.com",
                UserName = "test@example.com",
                FirstName = "Test",
                LastName = "User"
            };

            _mockUserManager.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "User" });

            // Act
            var token = await _service.GenerateTokenAsync(user);

            // Assert
            Assert.NotNull(token);
            Assert.NotEmpty(token);

            // Validate token structure
            var tokenHandler = new JwtSecurityTokenHandler();
            Assert.True(tokenHandler.CanReadToken(token));

            var jwtToken = tokenHandler.ReadJwtToken(token);
            Assert.Equal(_jwtSettings["JWT:Issuer"], jwtToken.Issuer);
            Assert.Contains(_jwtSettings["JWT:Audience"], jwtToken.Audiences);

            // Verify claims
            Assert.Equal(user.Id, jwtToken.Claims.First(x => x.Type == ClaimTypes.NameIdentifier).Value);
            Assert.Equal(user.Email, jwtToken.Claims.First(x => x.Type == ClaimTypes.Email).Value);
            Assert.Equal(user.UserName, jwtToken.Claims.First(x => x.Type == ClaimTypes.Name).Value);
            Assert.Equal(user.FirstName, jwtToken.Claims.First(x => x.Type == ClaimTypes.GivenName).Value);
            Assert.Equal(user.LastName, jwtToken.Claims.First(x => x.Type == ClaimTypes.Surname).Value);
            Assert.Equal("User", jwtToken.Claims.First(x => x.Type == ClaimTypes.Role).Value);

            // Verify expiration (should be ~24 hours from now)
            var expirationTime = jwtToken.ValidTo;
            var expectedExpiration = DateTime.UtcNow.AddHours(24);
            Assert.True(Math.Abs((expirationTime - expectedExpiration).TotalMinutes) < 1);

            // Verify logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("JWT token generated for user")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GenerateTokenAsync_UserWithMultipleRoles_IncludesAllRoles()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = "admin@example.com",
                UserName = "admin@example.com",
                FirstName = "Admin",
                LastName = "User"
            };

            var roles = new List<string> { "User", "Admin" };
            _mockUserManager.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(roles);

            // Act
            var token = await _service.GenerateTokenAsync(user);

            // Assert
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);

            var roleClaims = jwtToken.Claims.Where(x => x.Type == ClaimTypes.Role).Select(x => x.Value).ToList();
            Assert.Equal(2, roleClaims.Count);
            Assert.Contains("User", roleClaims);
            Assert.Contains("Admin", roleClaims);
        }

        [Fact]
        public async Task GenerateTokenAsync_UserWithoutNames_DoesNotIncludeNameClaims()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = "test@example.com",
                UserName = "test@example.com",
                FirstName = null,
                LastName = null
            };

            _mockUserManager.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "User" });

            // Act
            var token = await _service.GenerateTokenAsync(user);

            // Assert
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);

            Assert.DoesNotContain(jwtToken.Claims, c => c.Type == ClaimTypes.GivenName);
            Assert.DoesNotContain(jwtToken.Claims, c => c.Type == ClaimTypes.Surname);
        }

        [Fact]
        public async Task GenerateTokenAsync_ContainsRequiredClaims()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = "test@example.com",
                UserName = "test@example.com"
            };

            _mockUserManager.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "User" });

            // Act
            var token = await _service.GenerateTokenAsync(user);

            // Assert
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);

            // Verify required claims exist
            Assert.Contains(jwtToken.Claims, c => c.Type == ClaimTypes.NameIdentifier);
            Assert.Contains(jwtToken.Claims, c => c.Type == ClaimTypes.Email);
            Assert.Contains(jwtToken.Claims, c => c.Type == ClaimTypes.Name);
            Assert.Contains(jwtToken.Claims, c => c.Type == "jti"); // JWT ID
            Assert.Contains(jwtToken.Claims, c => c.Type == "iat"); // Issued at

            // Verify JTI is a valid GUID
            var jtiClaim = jwtToken.Claims.First(c => c.Type == "jti").Value;
            Assert.True(Guid.TryParse(jtiClaim, out _));

            // Verify IAT is a valid timestamp
            var iatClaim = jwtToken.Claims.First(c => c.Type == "iat").Value;
            Assert.True(long.TryParse(iatClaim, out _));
        }

        [Fact]
        public async Task GenerateRefreshTokenAsync_ReturnsBase64String()
        {
            // Act
            var refreshToken = await _service.GenerateRefreshTokenAsync();

            // Assert
            Assert.NotNull(refreshToken);
            Assert.NotEmpty(refreshToken);

            // Verify it's a valid base64 string
            try
            {
                var bytes = Convert.FromBase64String(refreshToken);
                Assert.Equal(64, bytes.Length); // Should be 64 random bytes
            }
            catch (FormatException)
            {
                Assert.Fail("Refresh token should be a valid base64 string");
            }
        }

        [Fact]
        public async Task GenerateRefreshTokenAsync_GeneratesUniqueTokens()
        {
            // Act
            var token1 = await _service.GenerateRefreshTokenAsync();
            var token2 = await _service.GenerateRefreshTokenAsync();

            // Assert
            Assert.NotEqual(token1, token2);
        }

        [Fact]
        public void ValidateToken_ValidToken_ReturnsTrue()
        {
            // Arrange - Generate a valid token first
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = "test@example.com",
                UserName = "test@example.com"
            };

            _mockUserManager.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "User" });

            var token = _service.GenerateTokenAsync(user).Result;

            // Act
            var isValid = _service.ValidateToken(token);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void ValidateToken_InvalidToken_ReturnsFalse()
        {
            // Arrange
            var invalidToken = "invalid.jwt.token";

            // Act
            var isValid = _service.ValidateToken(invalidToken);

            // Assert
            Assert.False(isValid);

            // Verify warning was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Token validation failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public void ValidateToken_ExpiredToken_ReturnsFalse()
        {
            // Arrange - Create a token that's already expired
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSettings["JWT:Secret"]);
            
            var tokenDescriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
                }),
                Expires = DateTime.UtcNow.AddMinutes(-1), // Already expired
                Issuer = _jwtSettings["JWT:Issuer"],
                Audience = _jwtSettings["JWT:Audience"],
                SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                    new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                    Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var expiredTokenString = tokenHandler.WriteToken(token);

            // Act
            var isValid = _service.ValidateToken(expiredTokenString);

            // Assert
            Assert.False(isValid);

            // Verify warning was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Token validation failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public void ValidateToken_WrongSigningKey_ReturnsFalse()
        {
            // Arrange - Create a token with wrong signing key
            var tokenHandler = new JwtSecurityTokenHandler();
            var wrongKey = Encoding.UTF8.GetBytes("WrongSecretKeyThatDoesNotMatchTheConfiguredKey123");
            
            var tokenDescriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
                }),
                Expires = DateTime.UtcNow.AddHours(1),
                Issuer = _jwtSettings["JWT:Issuer"],
                Audience = _jwtSettings["JWT:Audience"],
                SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                    new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(wrongKey),
                    Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var wronglySignedToken = tokenHandler.WriteToken(token);

            // Act
            var isValid = _service.ValidateToken(wronglySignedToken);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void ValidateToken_NullOrEmptyToken_ReturnsFalse()
        {
            // Act & Assert
            Assert.False(_service.ValidateToken(null!));
            Assert.False(_service.ValidateToken(string.Empty));
            Assert.False(_service.ValidateToken("   "));
        }
    }
}