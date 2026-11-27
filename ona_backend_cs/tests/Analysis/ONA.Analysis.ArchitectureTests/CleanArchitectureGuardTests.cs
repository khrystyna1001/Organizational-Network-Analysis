using System.Reflection;
using System.Runtime.CompilerServices;
using NetArchTest.Rules;
using ONA.Analysis.Application.Abstractions;
using ONA.Analysis.Domain.Aggregates;
using ONA.Analysis.Domain.Exceptions;
using ONA.Analysis.Infrastructure;

namespace ONA.Analysis.ArchitectureTests;

/// <summary>Fitness functions for the rules in AGENTS.md, applied to the Analysis context.</summary>
public sealed class CleanArchitectureGuardTests
{
    private static readonly Assembly DomainAssembly = typeof(AnalysisRun).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(IAnalysisRunRepository).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(DependencyInjection).Assembly;

    [Fact]
    public void Domain_ShouldNotDependOnOuterLayersOrPersistence()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "ONA.Analysis.Application", "ONA.Analysis.Infrastructure", "ONA.Analysis.Presentation",
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
                "ONA.Analysis.Infrastructure", "ONA.Analysis.Presentation",
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
            .That().ResideInNamespace("ONA.Analysis.Domain.Exceptions")
            .And().AreClasses().And().AreNotAbstract()
            .Should().Inherit(typeof(DomainException)).And().BeSealed()
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("Invalid domain exceptions", result));
    }

    [Fact]
    public void DomainExceptions_ShouldHaveSnakeCaseCodes()
    {
        var samples = new DomainException[]
        {
            new AnalysisRunNotFoundException("x"), new BoardNotFoundException("x"),
            new GraphNotPublishedException("x"), new InvalidNetworkGraphException("x"),
            new InvalidIdentifierException("x", null), new InvalidAnalysisRunStateTransitionException("a", "b")
        };

        Assert.All(samples, e => Assert.Matches("^[a-z]+(_[a-z]+)*(\\.[a-z]+(_[a-z]+)*)+$", e.Code));
    }

    [Fact]
    public void Infrastructure_RepositoriesDbContextAndAcl_ShouldNotBePublic()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .That().HaveNameEndingWith("Repository")
            .Or().HaveNameEndingWith("DbContext")
            .Or().ResideInNamespaceStartingWith("ONA.Analysis.Infrastructure.Acl")
            .Should().NotBePublic()
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("Infrastructure types must be internal", result));
    }

    [Fact]
    public void Infrastructure_AclClasses_ShouldBeSealed()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .That().ResideInNamespaceStartingWith("ONA.Analysis.Infrastructure.Acl")
            .And().AreClasses().And().DoNotHaveNameStartingWith("<")
            .Should().BeSealed()
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("ACL classes must be sealed", result));
    }

    [Fact]
    public void ForeignDtos_ShouldNotLeakIntoDomainOrApplication()
    {
        var leaked = new[] { DomainAssembly, ApplicationAssembly }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Name.EndsWith("_DTO", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToList();

        Assert.Empty(leaked);
    }

    [Fact]
    public void ApplicationPorts_ShouldBeInterfaces()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace("ONA.Analysis.Application.Ports")
            .And().HaveNameEndingWith("Port")
            .Should().BeInterfaces()
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("Ports must be interfaces", result));
    }

    private static string Describe(string message, TestResult result) =>
        $"{message}: {string.Join(", ", result.FailingTypeNames ?? [])}";
}
