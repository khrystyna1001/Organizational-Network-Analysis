using EchoAPI.Organization.Application.Abstractions;
using EchoAPI.Organization.Application.Services;
using EchoAPI.Organization.Infrastructure.ExternalServices;
using Microsoft.Extensions.DependencyInjection;

namespace EchoAPI.Organization.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<OrganizationDataService>();
        services.AddSingleton<IOrganizationDataReader, CsvOrganizationDataReader>();
        return services;
    }
}
