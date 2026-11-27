using System.Reflection;
using System.Runtime.CompilerServices;
using NetArchTest.Rules;
using ONA.Insights.Application.Abstractions;
using ONA.Insights.Domain.Aggregates;
using ONA.Insights.Domain.Exceptions;
using ONA.Insights.Infrastructure;

namespace ONA.Insights.ArchitectureTests.Architecture;

/// <summary>Fitness functions for the Insights context (rules from AGENTS.md).</summary>
public sealed class CleanArchitectureGuardTests
{
    private static readonly Assembly DomainAssembly = typeof(Recommendation).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(IRecommendationRepository).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(DependencyInjection).Assembly;

    [Fact]
    public void Domain_ShouldNotDependOnOuterLayersOrPersistence()
    {
        var result = Types.InAssembly(DomainAssembly).ShouldNot().HaveDependencyOnAny(
            "ONA.Insights.Application", "ONA.Insights.Infrastructure", "ONA.Insights.Presentation",
            "Microsoft.EntityFrameworkCore", "MySql", "Microsoft.AspNetCore").GetResult();

        Assert.True(result.IsSuccessful, Describe("Domain violates the Dependency Rule", result));
    }

    [Fact]
    public void Application_ShouldDependOnlyOnDomain()
    {
        var result = Types.InAssembly(ApplicationAssembly).ShouldNot().HaveDependencyOnAny(
            "ONA.Insights.Infrastructure", "ONA.Insights.Presentation",
            "Microsoft.EntityFrameworkCore", "MySql", "Microsoft.AspNetCore").GetResult();

        Assert.True(result.IsSuccessful, Describe("Application violates the Dependency Rule", result));
    }

    [Fact]
    public void Domain_ShouldNotHavePublicSetters()
    {
        var publicSetters = DomainAssembly.GetTypes()
            .Where(type => type.IsClass && !type.IsDefined(typeof(CompilerGeneratedAttribute), false))
            .Where(type => !typeof(Exception).IsAssignableFrom(type))
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Where(property => property.SetMethod is { IsPublic: true })
            .Select(property => $"{property.DeclaringType?.Name}.{property.Name}")
            .ToList();

        Assert.Empty(publicSetters);
    }

    [Fact]
    public void DomainExceptions_ShouldInheritDomainExceptionAndBeSealed()
    {
        var result = Types.InAssembly(DomainAssembly).That()
            .ResideInNamespace("ONA.Insights.Domain.Exceptions").And().AreClasses().And().AreNotAbstract()
            .Should().Inherit(typeof(DomainException)).And().BeSealed().GetResult();

        Assert.True(result.IsSuccessful, Describe("Invalid domain exceptions", result));
    }

    [Fact]
    public void DomainExceptionCodes_ShouldBeSnakeCase()
    {
        var codes = DomainAssembly.GetTypes()
            .Where(t => t.IsSealed && typeof(DomainException).IsAssignableFrom(t))
            .Select(t => t.GetConstructors().First())
            .Select(c => c.Invoke(c.GetParameters().Select(p => (object?)"x").ToArray()))
            .Cast<DomainException>()
            .Select(e => e.Code);

        Assert.All(codes, code => Assert.Matches("^[a-z]+(\\.[a-z_]+)+$", code));
    }

    [Fact]
    public void Infrastructure_RepositoriesDbContextAndAcl_ShouldNotBePublic()
    {
        var result = Types.InAssembly(InfrastructureAssembly).That()
            .HaveNameEndingWith("Repository").Or().HaveNameEndingWith("DbContext")
            .Or().ResideInNamespaceStartingWith("ONA.Insights.Infrastructure.Acl")
            .Or().ResideInNamespaceStartingWith("ONA.Insights.Infrastructure.Generation")
            .Should().NotBePublic().GetResult();

        Assert.True(result.IsSuccessful, Describe("Infrastructure types must be internal", result));
    }

    [Fact]
    public void Infrastructure_AclClasses_ShouldBeSealed()
    {
        var result = Types.InAssembly(InfrastructureAssembly).That()
            .ResideInNamespaceStartingWith("ONA.Insights.Infrastructure.Acl").And().AreClasses()
            .And().DoNotHaveNameStartingWith("<").Should().BeSealed().GetResult();

        Assert.True(result.IsSuccessful, Describe("ACL classes must be sealed", result));
    }

    [Fact]
    public void ForeignDtos_ShouldNotLeakIntoDomainOrApplication()
    {
        var leaked = new[] { DomainAssembly, ApplicationAssembly }
            .SelectMany(a => a.GetTypes())
            .Where(t => t.Name.EndsWith("_DTO", StringComparison.Ordinal))
            .Select(t => t.FullName)
            .ToList();
        var dependsOnAcl = Types.InAssemblies([DomainAssembly, ApplicationAssembly])
            .ShouldNot().HaveDependencyOn("ONA.Insights.Infrastructure.Acl").GetResult();

        Assert.Empty(leaked);
        Assert.True(dependsOnAcl.IsSuccessful, Describe("Foreign ACL types used inside", dependsOnAcl));
    }

    [Fact]
    public void ApplicationPorts_ShouldBeInterfaces()
    {
        var result = Types.InAssembly(ApplicationAssembly).That()
            .ResideInNamespace("ONA.Insights.Application.Ports").And().HaveNameEndingWith("Port")
            .Should().BeInterfaces().GetResult();

        Assert.True(result.IsSuccessful, Describe("Ports must be interfaces", result));
    }

    private static string Describe(string message, TestResult result) =>
        $"{message}: {string.Join(", ", result.FailingTypeNames ?? [])}";
}
