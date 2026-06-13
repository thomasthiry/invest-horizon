using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InvestHorizon.Infrastructure.Persistence;

public sealed class DatabaseSeeder
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IConfiguration _config;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(UserManager<AppUser> userManager, IConfiguration config, ILogger<DatabaseSeeder> logger)
    {
        _userManager = userManager;
        _config = config;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        var email = _config["Seed:UserEmail"] ?? "admin@investhorizon.local";
        var password = _config["Seed:UserPassword"] ?? "Admin1234!";

        if (await _userManager.FindByEmailAsync(email) is not null)
            return;

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, password);
        if (result.Succeeded)
            _logger.LogInformation("Seeded initial user {Email}", email);
        else
            _logger.LogWarning("Failed to seed user {Email}: {Errors}", email, string.Join(", ", result.Errors.Select(e => e.Description)));
    }
}
