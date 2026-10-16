using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using RetailOps.Application.Auth;
using RetailOps.Application.Catalog;
using RetailOps.Application.Inventory;
using RetailOps.Application.Reports;
using RetailOps.Application.Sales;
using RetailOps.Application.Transfers;

namespace RetailOps.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<AuthService>();
        services.AddScoped<StoreService>();
        services.AddScoped<ProductService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<SaleService>();
        services.AddScoped<TransferService>();
        services.AddScoped<ReportService>();
        return services;
    }
}
