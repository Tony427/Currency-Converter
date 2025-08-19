using Microsoft.AspNetCore.Identity;

namespace CurrencyConverter.API.Models;

public class ApplicationRole : IdentityRole
{
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}