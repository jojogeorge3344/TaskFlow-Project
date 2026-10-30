using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using NetArchTest.Rules;

namespace TaskFlow.UnitTests;

/// <summary>The backend-architecture rules (REQ-04 and REQ-05), checked against the compiled API and its source. Each rule
/// covers the layer folders (Controllers, Services, Repositories, Dtos, Middleware, Filters); code still waiting in Legacy
/// or in the flat Controllers folder is listed in <see cref="Pending"/> until its module moves.</summary>
public class ArchitectureTests
{
    private const string Root = "TaskFlow.Api";

    private static readonly Assembly Api = typeof(Program).Assembly;

    /// <summary>The module tree from the permission catalogue (design D2), plus the pre-sign-in Auth module. A layer folder
    /// may only contain these paths, each optionally followed by a Rules or Validators folder.</summary>
    private static readonly string[] ModulePaths =
    [
        "Auth", "Auth.Mfa", "Auth.PasswordRecovery",
        "Dashboard", "Dashboard.Analytics",
        "Monitoring.TaskManagement", "Monitoring.TaskManagement.Discussion", "Monitoring.TaskManagement.Activity",
        "Monitoring.Board", "Monitoring.EmployeeWorkload",
        "Management.EmployeeManagement", "Management.EmployeeManagement.Allocations",
        "Management.ProjectManagement", "Management.ProjectManagement.Positions",
        "Management.DepartmentManagement",
        "Communication.Mail", "Communication.Mail.Attachments",
        "Communication.Notification", "Communication.Notification.Reminders",
        "Reports",
        "Settings.ApplicationSettings", "Settings.ReminderSettings", "Settings.RoleConfiguration", "Settings.TfaSettings",
        "Profile",
        "Assistant",
    ];

    private static readonly string[] LayerFolders = ["Controllers", "Services", "Repositories", "Dtos"];

    /// <summary>Not migrated yet. Each entry names the task that removes it; task 12.1 deletes this list.</summary>
    private static readonly string[] Pending =
    [
        "TaskFlow.Application.",                              // Legacy/Application, groups 4-11
        "TaskFlow.Infrastructure.",                           // Legacy/Infrastructure, groups 4-11
        "TaskFlow.Api.Authorization.TaskAccessHandler",       // task 7.3
        "TaskFlow.Api.Authentication.SessionValidation",      // task 10.2
    ];

    private static bool IsPending(Type type) =>
        type.Namespace == $"{Root}.Controllers" // flat controllers folder, moved module by module in groups 4-11
        || Pending.Any(prefix => (type.FullName ?? string.Empty).StartsWith(prefix, StringComparison.Ordinal));

    private static IEnumerable<Type> TypesIn(string layer) =>
        Api.GetTypes().Where(type => type.Namespace is { } ns
                                     && (ns == $"{Root}.{layer}" || ns.StartsWith($"{Root}.{layer}.", StringComparison.Ordinal))
                                     && !IsPending(type)
                                     && !type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));

    // ---------------------------------------------------------------- one-way layered flow

    [Fact]
    public void ControllersDoNotReachRepositoriesTheDatabaseOrIntegrations()
    {
        AssertNoDependency(
            $"{Root}.Controllers",
            "Microsoft.EntityFrameworkCore",
            $"{Root}.Persistence",
            $"{Root}.Repositories",
            $"{Root}.Integrations",
            "TaskFlow.Application.Abstractions.IApplicationDbContext");
    }

    [Fact]
    public void ServicesDoNotReachTheDatabaseOrControllers()
    {
        AssertNoDependency(
            $"{Root}.Services",
            "Microsoft.EntityFrameworkCore",
            $"{Root}.Persistence",
            $"{Root}.Controllers",
            "TaskFlow.Application.Abstractions.IApplicationDbContext");
    }

    [Fact]
    public void ServicesUseRepositoriesOnlyThroughTheirInterfaces()
    {
        var concreteRepositories = TypesIn("Repositories")
            .Where(type => type is { IsClass: true, IsAbstract: false }
                           && type.GetInterfaces().Any(contract => contract.Name.EndsWith("Repository", StringComparison.Ordinal)
                                                                   || contract.Name.EndsWith("Repository`1", StringComparison.Ordinal)
                                                                   || contract.Name == "IUnitOfWork"))
            .Select(type => type.FullName!)
            .ToArray();

        if (concreteRepositories.Length > 0)
        {
            AssertNoDependency($"{Root}.Services", concreteRepositories);
        }

        var offenders = TypesIn("Services")
            .SelectMany(type => type.GetConstructors().SelectMany(ctor => ctor.GetParameters())
                .Where(parameter => parameter.ParameterType.Namespace?.StartsWith($"{Root}.Repositories", StringComparison.Ordinal) == true
                                    && !parameter.ParameterType.IsInterface)
                .Select(parameter => $"{type.FullName}({parameter.ParameterType.Name})"))
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void RequestPipelineComponentsDoNotReachTheDatabase()
    {
        foreach (var layer in new[] { "Middleware", "Filters", "Authentication", "Authorization" })
        {
            AssertNoDependency(
                $"{Root}.{layer}",
                $"{Root}.Persistence.AppDbContext",
                "TaskFlow.Application.Abstractions.IApplicationDbContext",
                "Microsoft.EntityFrameworkCore.DbContext");
        }
    }

    // ---------------------------------------------------------------- controllers handle HTTP only

    [Fact]
    public void ControllersHaveNoExceptionHandling()
    {
        var offenders = SourceFiles("Controllers")
            .Where(file => Regex.IsMatch(StripComments(File.ReadAllText(file)), @"\btry\s*\{|\bcatch\s*[\({]"))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void ControllersDoNotValidateRequestsThemselves()
    {
        var offenders = Types.InAssembly(Api)
            .That().Inherit(typeof(ControllerBase))
            .Should().NotHaveDependencyOn("FluentValidation")
            .GetResult().FailingTypeNames ?? [];

        Assert.Empty(offenders);
    }

    [Fact]
    public void EntitiesNeverCrossTheHttpBoundary()
    {
        var offenders = new List<string>();
        foreach (var controller in TypesIn("Controllers").Where(type => typeof(ControllerBase).IsAssignableFrom(type)))
        {
            foreach (var action in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                var types = action.GetParameters().Select(parameter => parameter.ParameterType).Append(action.ReturnType);
                offenders.AddRange(types.Where(ContainsEntity).Select(_ => $"{controller.Name}.{action.Name}"));
            }
        }

        Assert.Empty(offenders.Distinct());
    }

    // ---------------------------------------------------------------- repositories

    [Fact]
    public void RepositoriesNeverHandOutDeferredQueries()
    {
        var offenders = TypesIn("Repositories")
            .Where(type => type.IsInterface)
            .SelectMany(type => type.GetMethods().Concat(type.GetInterfaces().SelectMany(parent => parent.GetMethods()))
                .Where(method => ReturnsQuery(method.ReturnType))
                .Select(method => $"{type.Name}.{method.Name}"))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void OnlyTheUnitOfWorkCommits()
    {
        var offenders = SourceFiles("Repositories").Append(Path.Combine(SourceRoot, "Common", "BaseRepository.cs"))
            .Where(file => Path.GetFileName(file) is not ("UnitOfWork.cs" or "IUnitOfWork.cs"))
            .Where(file => Regex.IsMatch(StripComments(File.ReadAllText(file)), @"\bSaveChanges(Async)?\s*\(|\bBeginTransaction"))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    // ---------------------------------------------------------------- folders, modules and names

    [Fact]
    public void LayerNamespacesFollowTheModuleTree()
    {
        var offenders = LayerFolders
            .SelectMany(layer => TypesIn(layer).Select(type => (layer, type)))
            .Where(item => !IsAllowedModulePath(ModulePathOf(item.type.Namespace!, item.layer)))
            .Select(item => item.type.FullName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void EveryLayerFileDeclaresTheNamespaceOfItsFolder()
    {
        var offenders = new List<string>();
        foreach (var layer in LayerFolders.Concat(["Middleware", "Filters", "Config", "Common", "Models"]))
        {
            foreach (var file in SourceFiles(layer))
            {
                var folder = Path.GetRelativePath(SourceRoot, Path.GetDirectoryName(file)!).Replace(Path.DirectorySeparatorChar, '.');
                var expected = $"namespace {Root}.{folder};";
                if (!File.ReadAllText(file).Contains(expected, StringComparison.Ordinal))
                {
                    offenders.Add($"{Path.GetRelativePath(SourceRoot, file)} (expected '{expected}')");
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void LayerTypesAreNamedForTheirLayer()
    {
        var offenders = new List<string>();

        offenders.AddRange(Api.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .Where(type => !type.Name.EndsWith("Controller", StringComparison.Ordinal) || type.Namespace?.StartsWith($"{Root}.Controllers", StringComparison.Ordinal) != true)
            .Select(type => type.FullName!));

        offenders.AddRange(NamingOffenders("Services", "Service"));
        offenders.AddRange(NamingOffenders("Repositories", "Repository", allowed: ["UnitOfWork", "IUnitOfWork", "IBaseRepository`1"]));

        Assert.Empty(offenders);
    }

    [Fact]
    public void NoEmptyFolders()
    {
        var empty = Directory.EnumerateDirectories(SourceRoot, "*", SearchOption.AllDirectories)
            .Where(directory => !IsBuildOutput(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
            .Select(directory => Path.GetRelativePath(SourceRoot, directory))
            .ToList();

        Assert.Empty(empty);
    }

    // ---------------------------------------------------------------- helpers

    private static void AssertNoDependency(string layerNamespace, params string[] forbidden)
    {
        var offenders = Types.InAssembly(Api)
            .That().ResideInNamespace(layerNamespace)
            .Should().NotHaveDependencyOnAny(forbidden)
            .GetResult().FailingTypeNames ?? [];

        Assert.Empty(offenders.Where(name => !IsPending(Api.GetType(name.Replace('/', '+')) ?? typeof(object))
                                             && !name.Contains('<')));
    }

    private static IEnumerable<string> NamingOffenders(string layer, string suffix, string[]? allowed = null)
    {
        allowed ??= [];
        return TypesIn(layer)
            .Where(type => !type.IsNested && type.IsPublic && !allowed.Contains(type.Name))
            .Where(type => type.Namespace!.Split('.') is var parts && parts[^1] is not ("Rules" or "Validators"))
            // A service (or repository) type is one that fulfils an I<Name>Service (or I<Name>Repository) contract. Its
            // collaborators in the same folder (a clock, a publisher, a scope) keep names that say what they are.
            .Where(type => type.IsInterface
                ? !type.Name.StartsWith('I')
                : type.GetInterfaces().Any(contract => contract.Name.EndsWith(suffix, StringComparison.Ordinal))
                  && !type.Name.EndsWith(suffix, StringComparison.Ordinal))
            .Select(type => type.FullName!);
    }

    private static string ModulePathOf(string ns, string layer)
    {
        var path = ns[$"{Root}.{layer}".Length..].TrimStart('.');
        if (path == "Interfaces" || path.StartsWith("Interfaces.", StringComparison.Ordinal))
        {
            path = path["Interfaces".Length..].TrimStart('.');
        }

        return Regex.Replace(path, @"\.(Rules|Validators)$", string.Empty);
    }

    private static bool IsAllowedModulePath(string path) => path.Length == 0 || ModulePaths.Contains(path);

    private static bool ContainsEntity(Type type) =>
        type.Namespace == $"{Root}.Models.Entities"
        || (type.IsGenericType && type.GetGenericArguments().Any(ContainsEntity))
        || (type.IsArray && ContainsEntity(type.GetElementType()!));

    private static bool ReturnsQuery(Type type) =>
        typeof(IQueryable).IsAssignableFrom(type)
        || (type.IsGenericType && type.GetGenericArguments().Any(ReturnsQuery));

    private static string StripComments(string source) =>
        Regex.Replace(source, @"//[^\n]*|/\*.*?\*/", string.Empty, RegexOptions.Singleline);

    private static IEnumerable<string> SourceFiles(string folder)
    {
        var path = Path.Combine(SourceRoot, folder);
        return Directory.Exists(path)
            ? Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories).Where(file => !IsBuildOutput(file))
            : [];
    }

    private static bool IsBuildOutput(string path)
    {
        var relative = Path.GetRelativePath(SourceRoot, path).Split(Path.DirectorySeparatorChar);
        return relative[0] is "bin" or "obj" or "Properties";
    }

    /// <summary>The API's source folder, found by walking up from the test output to the solution.</summary>
    private static string SourceRoot { get; } = FindSourceRoot();

    private static string FindSourceRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TaskFlow.sln")))
            {
                return Path.Combine(directory.FullName, "src", Root);
            }
        }

        throw new InvalidOperationException("TaskFlow.sln was not found above the test output folder.");
    }
}
