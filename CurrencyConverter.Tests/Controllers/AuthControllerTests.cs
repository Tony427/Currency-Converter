using CurrencyConverter.API.Controllers;
using CurrencyConverter.Application.DTOs.Auth;
using CurrencyConverter.Application.Services;
using CurrencyConverter.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace CurrencyConverter.Tests.Controllers
{
    public class AuthControllerTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
        private readonly Mock<RoleManager<ApplicationRole>> _mockRoleManager;
        private readonly Mock<IJwtTokenService> _mockJwtTokenService;
        private readonly Mock<ILogger<AuthController>> _mockLogger;
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            // Mock UserManager
            var userStore = new Mock<IUserStore<ApplicationUser>>();
            _mockUserManager = new Mock<UserManager<ApplicationUser>>(
                userStore.Object, null, null, null, null, null, null, null, null);

            // Mock RoleManager
            var roleStore = new Mock<IRoleStore<ApplicationRole>>();
            _mockRoleManager = new Mock<RoleManager<ApplicationRole>>(
                roleStore.Object, null, null, null, null);

            // Mock SignInManager
            _mockSignInManager = new Mock<SignInManager<ApplicationUser>>(
                _mockUserManager.Object,
                Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(),
                Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
                null, null, null, null);

            _mockJwtTokenService = new Mock<IJwtTokenService>();
            _mockLogger = new Mock<ILogger<AuthController>>();

            _controller = new AuthController(
                _mockUserManager.Object,
                _mockSignInManager.Object,
                _mockRoleManager.Object,
                _mockJwtTokenService.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task Register_ValidRequest_ReturnsAuthResponse()
        {
            // Arrange
            var request = new RegisterRequestDto
            {
                Email = "test@example.com",
                Password = "Test123!",
                FirstName = "Test",
                LastName = "User"
            };

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName
            };

            _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
                .ReturnsAsync((ApplicationUser?)null);

            _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
                .ReturnsAsync(IdentityResult.Success);

            _mockUserManager.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), "User"))
                .ReturnsAsync(IdentityResult.Success);

            _mockUserManager.Setup(x => x.GetRolesAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync(new List<string> { "User" });

            _mockJwtTokenService.Setup(x => x.GenerateTokenAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync("test-jwt-token");

            _mockJwtTokenService.Setup(x => x.GenerateRefreshTokenAsync())
                .ReturnsAsync("test-refresh-token");

            // Act
            var result = await _controller.Register(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var authResponse = Assert.IsType<AuthResponseDto>(okResult.Value);
            
            Assert.Equal("test-jwt-token", authResponse.Token);
            Assert.Equal("test-refresh-token", authResponse.RefreshToken);
            Assert.Equal(request.Email, authResponse.User.Email);
            Assert.Equal(request.FirstName, authResponse.User.FirstName);
            Assert.Equal(request.LastName, authResponse.User.LastName);
            Assert.Contains("User", authResponse.User.Roles);

            // Verify logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Registration attempt for email")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("User registered successfully")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Register_ExistingUser_ReturnsBadRequest()
        {
            // Arrange
            var request = new RegisterRequestDto
            {
                Email = "existing@example.com",
                Password = "Test123!",
                FirstName = "Test",
                LastName = "User"
            };

            var existingUser = new ApplicationUser { Email = request.Email };

            _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
                .ReturnsAsync(existingUser);

            // Act
            var result = await _controller.Register(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.NotNull(badRequestResult.Value);

            // Verify user creation was not attempted
            _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Register_CreateUserFails_ReturnsBadRequest()
        {
            // Arrange
            var request = new RegisterRequestDto
            {
                Email = "test@example.com",
                Password = "weak",
                FirstName = "Test",
                LastName = "User"
            };

            _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
                .ReturnsAsync((ApplicationUser?)null);

            var errors = new List<IdentityError>
            {
                new IdentityError { Code = "PasswordTooShort", Description = "Password too short" }
            };

            _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
                .ReturnsAsync(IdentityResult.Failed(errors.ToArray()));

            // Act
            var result = await _controller.Register(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.NotNull(badRequestResult.Value);
        }

        [Fact]
        public async Task Login_ValidCredentials_ReturnsAuthResponse()
        {
            // Arrange
            var request = new LoginRequestDto
            {
                Email = "test@example.com",
                Password = "Test123!"
            };

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = request.Email,
                FirstName = "Test",
                LastName = "User",
                IsActive = true
            };

            _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
                .ReturnsAsync(user);

            _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, request.Password, false))
                .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

            _mockUserManager.Setup(x => x.UpdateAsync(user))
                .ReturnsAsync(IdentityResult.Success);

            _mockUserManager.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "User" });

            _mockJwtTokenService.Setup(x => x.GenerateTokenAsync(user))
                .ReturnsAsync("test-jwt-token");

            _mockJwtTokenService.Setup(x => x.GenerateRefreshTokenAsync())
                .ReturnsAsync("test-refresh-token");

            // Act
            var result = await _controller.Login(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var authResponse = Assert.IsType<AuthResponseDto>(okResult.Value);
            
            Assert.Equal("test-jwt-token", authResponse.Token);
            Assert.Equal("test-refresh-token", authResponse.RefreshToken);
            Assert.Equal(request.Email, authResponse.User.Email);

            // Verify last login was updated
            _mockUserManager.Verify(x => x.UpdateAsync(It.Is<ApplicationUser>(u => u.LastLoginAt.HasValue)), Times.Once);

            // Verify logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Login attempt for email")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("User logged in successfully")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Login_UserNotFound_ReturnsUnauthorized()
        {
            // Arrange
            var request = new LoginRequestDto
            {
                Email = "nonexistent@example.com",
                Password = "Test123!"
            };

            _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
                .ReturnsAsync((ApplicationUser?)null);

            // Act
            var result = await _controller.Login(request);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
            Assert.NotNull(unauthorizedResult.Value);

            // Verify password check was not attempted
            _mockSignInManager.Verify(x => x.CheckPasswordSignInAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task Login_InactiveUser_ReturnsUnauthorized()
        {
            // Arrange
            var request = new LoginRequestDto
            {
                Email = "inactive@example.com",
                Password = "Test123!"
            };

            var inactiveUser = new ApplicationUser
            {
                Email = request.Email,
                IsActive = false
            };

            _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
                .ReturnsAsync(inactiveUser);

            // Act
            var result = await _controller.Login(request);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
            Assert.NotNull(unauthorizedResult.Value);
        }

        [Fact]
        public async Task Login_InvalidPassword_ReturnsUnauthorized()
        {
            // Arrange
            var request = new LoginRequestDto
            {
                Email = "test@example.com",
                Password = "WrongPassword"
            };

            var user = new ApplicationUser
            {
                Email = request.Email,
                IsActive = true
            };

            _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
                .ReturnsAsync(user);

            _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, request.Password, false))
                .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

            // Act
            var result = await _controller.Login(request);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
            Assert.NotNull(unauthorizedResult.Value);

            // Verify warning log for failed attempt
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed login attempt for email")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetProfile_ValidUser_ReturnsUserInfo()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();
            var user = new ApplicationUser
            {
                Id = userId,
                Email = "test@example.com",
                FirstName = "Test",
                LastName = "User"
            };

            // Setup controller with user claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId)
            };
            var identity = new ClaimsIdentity(claims);
            var principal = new ClaimsPrincipal(identity);
            _controller.ControllerContext.HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = principal
            };

            _mockUserManager.Setup(x => x.FindByIdAsync(userId))
                .ReturnsAsync(user);

            _mockUserManager.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "User" });

            // Act
            var result = await _controller.GetProfile();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var userInfo = Assert.IsType<UserInfoDto>(okResult.Value);
            
            Assert.Equal(user.Id, userInfo.Id);
            Assert.Equal(user.Email, userInfo.Email);
            Assert.Equal(user.FirstName, userInfo.FirstName);
            Assert.Equal(user.LastName, userInfo.LastName);
            Assert.Contains("User", userInfo.Roles);
        }

        [Fact]
        public async Task GetProfile_NoUserClaim_ReturnsUnauthorized()
        {
            // Arrange
            _controller.ControllerContext.HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new ClaimsPrincipal()
            };

            // Act
            var result = await _controller.GetProfile();

            // Assert
            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        [Fact]
        public async Task GetProfile_UserNotFound_ReturnsNotFound()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId)
            };
            var identity = new ClaimsIdentity(claims);
            var principal = new ClaimsPrincipal(identity);
            _controller.ControllerContext.HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = principal
            };

            _mockUserManager.Setup(x => x.FindByIdAsync(userId))
                .ReturnsAsync((ApplicationUser?)null);

            // Act
            var result = await _controller.GetProfile();

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.NotNull(notFoundResult.Value);
        }
    }
}