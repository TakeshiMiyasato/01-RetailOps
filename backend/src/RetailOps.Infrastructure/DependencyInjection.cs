using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RetailOps.Application.Common;
using RetailOps.Domain.Entities;
using RetailOps.Infrastructure.Auth;
using RetailOps.Infrastructure.Persistence;

namespace RetailOps.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("RetailOps")
                               ?? throw new InvalidOperationException("Connection string 'RetailOps' is missing.");

        services.AddDbContext<AppDbContext>(o => o
            .UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(maxRetryCount: 5);
                sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            })
            // Sale/transfer lines are only ever reached through their tenant-filtered parent, so the
            // "required navigation to a filtered entity" warning does not apply here.
            .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<DataSeeder>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.Section))
            .Validate(o => o.SigningKey.Length >= 32, "Jwt:SigningKey must be at least 32 characters.")
            .ValidateOnStart();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        return services;
    }
}
