using System.Reflection;
using System.Runtime.CompilerServices;
using NetArchTest.Rules;
using ONA.Graph.Application.Abstractions;
using ONA.Graph.Domain.Aggregates;
using ONA.Graph.Domain.Exceptions;
using ONA.Graph.Infrastructure;

namespace ONA.Graph.ArchitectureTests.Architecture;

/// <summary>
/// Architectural fitness functions (Ford et al.). They inspect the compiled assemblies on every
/// `dotnet test` run and fail when a human or an AI agent breaks a rule from AGENTS.md.
/// </summary>
public sealed class CleanArchitectureGuardTests
{
    private static readonly Assembly DomainAssembly = typeof(Board).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(IBoardRepository).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(DependencyInjection).Assembly;

    // Rule 1
    [Fact]
    public void Domain_ShouldNotDependOnOuterLayersOrPersistence()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "ONA.Graph.Application",
                "ONA.Graph.Infrastructure",
                "ONA.Graph.Presentation",
                "Microsoft.EntityFrameworkCore",
                "MySql",
                "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("Domain violates the Dependency Rule", result));
    }

    // Rule 1b
    [Fact]
    public void Application_ShouldDependOnlyOnDomain()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "ONA.Graph.Infrastructure",
                "ONA.Graph.Presentation",
                "Microsoft.EntityFrameworkCore",
                "MySql",
                "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("Application violates the Dependency Rule", result));
    }

    // Rule 2
    [Fact]
    public void Domain_EntitiesAggregatesAndValueObjects_ShouldNotHavePublicSetters()
    {
        var publicSetters = DomainAssembly.GetTypes()
            .Where(type => type.IsClass && !type.IsDefined(typeof(CompilerGeneratedAttribute), false))
            .Where(type => !typeof(Exception).IsAssignableFrom(type))
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Where(property => property.SetMethod is { IsPublic: true }) // also catches `init`
            .Select(property => $"{property.DeclaringType?.Name}.{property.Name}")
            .ToList();

        Assert.Empty(publicSetters);
    }

    // Rule 3
    [Fact]
    public void DomainExceptions_ShouldInheritDomainExceptionAndBeSealed()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .ResideInNamespace("ONA.Graph.Domain.Exceptions")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .Should()
            .Inherit(typeof(DomainException))
            .And()
            .BeSealed()
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("Invalid domain exceptions", result));
    }

    // Rule 4
    [Fact]
    public void Infrastructure_RepositoriesDbContextAndAcl_ShouldNotBePublic()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .That()
            .HaveNameEndingWith("Repository")
            .Or()
            .HaveNameEndingWith("DbContext")
            .Or()
            .HaveNameEndingWith("Seeder")
            .Or()
            .ResideInNamespaceStartingWith("ONA.Graph.Infrastructure.Acl")
            .Should()
            .NotBePublic()
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("Infrastructure types must be internal", result));
    }

    // Rule 4b
    [Fact]
    public void Infrastructure_AclClasses_ShouldBeSealed()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .That()
            .ResideInNamespaceStartingWith("ONA.Graph.Infrastructure.Acl")
            .And()
            .AreClasses()
            .And()
            .DoNotHaveNameStartingWith("<")
            .Should()
            .BeSealed()
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("ACL classes must be sealed", result));
    }

    // Rule 5
    [Fact]
    public void ForeignDtos_ShouldNotLeakIntoDomainOrApplication()
    {
        var leaked = new[] { DomainAssembly, ApplicationAssembly }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Name.EndsWith("_DTO", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToList();

        var dependsOnAcl = Types.InAssemblies([DomainAssembly, ApplicationAssembly])
            .ShouldNot()
            .HaveDependencyOn("ONA.Graph.Infrastructure.Acl")
            .GetResult();

        Assert.Empty(leaked);
        Assert.True(dependsOnAcl.IsSuccessful, Describe("Foreign ACL types used inside", dependsOnAcl));
    }

    // Rule 6
    [Fact]
    public void ApplicationPorts_ShouldBeInterfaces()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespace("ONA.Graph.Application.Ports")
            .And()
            .HaveNameEndingWith("Port")
            .Should()
            .BeInterfaces()
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("Ports must be interfaces", result));
    }

    private static string Describe(string message, TestResult result) =>
        $"{message}: {string.Join(", ", result.FailingTypeNames ?? [])}";
}
