using ONA.Organization.Application.Abstractions;
using ONA.Organization.Application.Services;
using ONA.Organization.Infrastructure.ExternalServices;
using Microsoft.Extensions.DependencyInjection;

namespace ONA.Organization.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<OrganizationDataService>();
        services.AddSingleton<IOrganizationDataReader, CsvOrganizationDataReader>();
        return services;
    }
}
