using System.Reflection;
using System.Runtime.CompilerServices;
using NetArchTest.Rules;
using ONA.Identity.Application.Abstractions;
using ONA.Identity.Domain.Aggregates;
using ONA.Identity.Domain.Exceptions;
using ONA.Identity.Infrastructure;

namespace ONA.Identity.ArchitectureTests.Architecture;

/// <summary>Fitness functions for AGENTS.md rules, applied to the Identity context.</summary>
public sealed class IdentityArchitectureGuardTests
{
    private static readonly Assembly DomainAssembly = typeof(User).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(IUserRepository).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(DependencyInjection).Assembly;

    [Fact]
    public void Domain_ShouldNotDependOnOuterLayersOrPersistence()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "ONA.Identity.Application", "ONA.Identity.Infrastructure", "ONA.Identity.Presentation",
                "Microsoft.EntityFrameworkCore", "MySql", "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("Domain violates the Dependency Rule", result));
    }

    [Fact]
    public void Application_ShouldDependOnlyOnDomain()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "ONA.Identity.Infrastructure", "ONA.Identity.Presentation",
                "Microsoft.EntityFrameworkCore", "MySql", "Microsoft.AspNetCore")
            .GetResult();

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
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace("ONA.Identity.Domain.Exceptions")
            .And().AreClasses().And().AreNotAbstract()
            .Should().Inherit(typeof(DomainException)).And().BeSealed()
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("Invalid domain exceptions", result));
    }

    [Fact]
    public void DomainExceptionCodes_ShouldBeSnakeCase()
    {
        var codes = DomainAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(DomainException).IsAssignableFrom(t))
            .Select(t => ((DomainException)Activator.CreateInstance(t, Enumerable.Repeat<object?>("x", t.GetConstructors()[0].GetParameters().Length).ToArray())!).Code)
            .ToList();

        Assert.NotEmpty(codes);
        Assert.All(codes, code => Assert.Matches("^[a-z]+(\\.[a-z_]+)+$", code));
    }

    [Fact]
    public void Infrastructure_RepositoriesDbContextAndSecurity_ShouldNotBePublic()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .That().HaveNameEndingWith("Repository")
            .Or().HaveNameEndingWith("DbContext")
            .Or().HaveNameEndingWith("Hasher")
            .Should().NotBePublic()
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("Infrastructure types must be internal", result));
    }

    [Fact]
    public void ApplicationAbstractions_ShouldBeInterfaces()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace("ONA.Identity.Application.Abstractions")
            .Should().BeInterfaces()
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("Abstractions must be interfaces", result));
    }

    [Fact]
    public void UserDto_ShouldNotExposePasswordHash()
    {
        var leaked = ApplicationAssembly.GetTypes()
            .Where(t => t.Namespace == "ONA.Identity.Application.DTOs")
            .SelectMany(t => t.GetProperties())
            .Where(p => p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase))
            .Select(p => $"{p.DeclaringType?.Name}.{p.Name}");

        Assert.Empty(leaked);
    }

    private static string Describe(string message, TestResult result) =>
        $"{message}: {string.Join(", ", result.FailingTypeNames ?? [])}";
}
