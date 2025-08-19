using CurrencyConverter.API.Models;

namespace CurrencyConverter.API.Services;

public interface IJwtTokenService
{
    Task<string> GenerateTokenAsync(ApplicationUser user);
    Task<string> GenerateRefreshTokenAsync();
    bool ValidateToken(string token);
}