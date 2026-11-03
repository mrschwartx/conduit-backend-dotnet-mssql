using System.Reflection;
using NetArchTest.Rules;
using Conduit.Modules.Articles;
using Conduit.Modules.Comments;
using Conduit.Modules.Users;
using Conduit.SharedKernel;

namespace Conduit.ArchitectureTests;

public sealed class ModuleBoundaryTests
{
    private static readonly string[] Frameworks =
    [
        "Microsoft.AspNetCore",
        "Microsoft.Data.SqlClient",
        "BCrypt",
        "Microsoft.IdentityModel"
    ];

    public static TheoryData<string, Assembly> Modules => new()
    {
        { "Users", typeof(UsersModule).Assembly },
        { "Articles", typeof(ArticlesModule).Assembly },
        { "Comments", typeof(CommentsModule).Assembly }
    };

    [Theory]
    [MemberData(nameof(Modules))]
    public void DomainDependsOnNeitherOuterLayersNorFrameworks(string module, Assembly assembly)
    {
        var forbidden = Layers(module, "Application", "Infrastructure", "Presentation")
            .Concat(Frameworks)
            .Append("Conduit.SharedKernel.Infrastructure")
            .ToArray();

        AssertNoDependencies(assembly, $"Conduit.Modules.{module}.Domain", forbidden);
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void ApplicationDependsOnNeitherInfrastructureNorPresentation(string module, Assembly assembly)
    {
        var forbidden = Layers(module, "Infrastructure", "Presentation")
            .Concat(Frameworks)
            .Append("Conduit.SharedKernel.Infrastructure")
            .ToArray();

        AssertNoDependencies(assembly, $"Conduit.Modules.{module}.Application", forbidden);
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void InfrastructureDoesNotDependOnPresentation(string module, Assembly assembly)
    {
        AssertNoDependencies(assembly, $"Conduit.Modules.{module}.Infrastructure", Layers(module, "Presentation"));
    }

    [Fact]
    public void ModulesDoNotReferenceEachOther()
    {
        var modules = new Dictionary<string, Assembly>
        {
            ["Users"] = typeof(UsersModule).Assembly,
            ["Articles"] = typeof(ArticlesModule).Assembly,
            ["Comments"] = typeof(CommentsModule).Assembly
        };

        foreach (var (name, assembly) in modules)
        {
            var others = modules.Keys.Where(other => other != name).Select(other => $"Conduit.Modules.{other}").ToArray();
            AssertAssemblyHasNoDependencies(assembly, others);

            var references = assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToList();
            foreach (var other in others)
            {
                Assert.DoesNotContain(other, references);
            }
        }
    }

    [Fact]
    public void ModulesDoNotReferenceTheHost()
    {
        foreach (var assembly in new[] { typeof(UsersModule).Assembly, typeof(ArticlesModule).Assembly, typeof(CommentsModule).Assembly, typeof(SharedKernelServiceCollectionExtensions).Assembly })
        {
            Assert.DoesNotContain("Conduit.Host", assembly.GetReferencedAssemblies().Select(name => name.Name));
        }
    }

    [Fact]
    public void SharedKernelDoesNotDependOnAnyModule()
    {
        AssertAssemblyHasNoDependencies(
            typeof(SharedKernelServiceCollectionExtensions).Assembly,
            ["Conduit.Modules", "Conduit.Host"]);
    }

    [Fact]
    public void SharedKernelDomainStaysFreeOfInfrastructure()
    {
        AssertNoDependencies(
            typeof(SharedKernelServiceCollectionExtensions).Assembly,
            "Conduit.SharedKernel.Domain",
            ["Conduit.SharedKernel.Infrastructure", "Conduit.SharedKernel.Application", "Microsoft.AspNetCore", "Microsoft.Data.SqlClient"]);
    }

    private static string[] Layers(string module, params string[] layers) =>
        layers.Select(layer => $"Conduit.Modules.{module}.{layer}").ToArray();

    private static void AssertAssemblyHasNoDependencies(Assembly assembly, string[] forbidden)
    {
        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"{assembly.GetName().Name} has forbidden dependencies: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    private static void AssertNoDependencies(Assembly assembly, string @namespace, string[] forbidden)
    {
        var result = Types.InAssembly(assembly)
            .That()
            .ResideInNamespace(@namespace)
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"{@namespace} has forbidden dependencies: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
