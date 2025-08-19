using CurrencyConverter.Domain.Entities;

namespace CurrencyConverter.Application.Services;

public interface IJwtTokenService
{
    Task<string> GenerateTokenAsync(ApplicationUser user);
    Task<string> GenerateRefreshTokenAsync();
    bool ValidateToken(string token);
}