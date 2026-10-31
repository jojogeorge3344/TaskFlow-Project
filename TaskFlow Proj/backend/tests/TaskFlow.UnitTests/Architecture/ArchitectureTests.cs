using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using NetArchTest.Rules;

namespace TaskFlow.UnitTests.Architecture;

/// <summary>The backend-architecture rules (REQ-04 and REQ-05), checked against the compiled API and its source. Each rule
/// covers the layer folders (Controllers, Services, Repositories, Dtos, Middleware, Filters), and the whole-assembly rules
/// keep the DbContext inside Persistence and Repositories and every type inside a known top-level folder.</summary>
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

    private static IEnumerable<Type> TypesIn(string layer) =>
        Api.GetTypes().Where(type => type.Namespace is { } ns
                                     && (ns == $"{Root}.{layer}" || ns.StartsWith($"{Root}.{layer}.", StringComparison.Ordinal))
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
            $"{Root}.Integrations");
    }

    [Fact]
    public void ServicesDoNotReachTheDatabaseOrControllers()
    {
        AssertNoDependency(
            $"{Root}.Services",
            "Microsoft.EntityFrameworkCore",
            $"{Root}.Persistence",
            $"{Root}.Controllers");
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
                // The contract records beside a repository interface (a criteria, a row) are fine; a repository class is not.
                .Where(parameter => concreteRepositories.Contains(parameter.ParameterType.FullName))
                .Select(parameter => $"{type.FullName}({parameter.ParameterType.Name})"))
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void RequestPipelineComponentsDoNotReachTheDatabase()
    {
        foreach (var layer in new[] { "Middleware", "Filters", "Authorization" })
        {
            AssertNoDependency(
                $"{Root}.{layer}",
                $"{Root}.Persistence.AppDbContext",
                "Microsoft.EntityFrameworkCore.DbContext");
        }
    }

    // ---------------------------------------------------------------- the whole assembly

    /// <summary>Folders a type may live in. Everything else is a layer, the composition root (<c>Program</c>, <c>Config</c>)
    /// or persistence.</summary>
    private static readonly string[] TopLevelFolders =
    [
        "Authorization", "Common", "Config", "Controllers", "Dtos", "Filters", "Integrations", "Middleware", "Models",
        "Persistence", "Repositories", "Services",
    ];

    [Fact]
    public void OnlyPersistenceAndRepositoriesReachTheDbContext()
    {
        // Config registers the DbContext and Program migrates and seeds at startup; BaseRepository is the repositories' base.
        var offenders = Types.InAssembly(Api)
            .That().DoNotResideInNamespaceMatching($@"^{Regex.Escape(Root)}\.(Persistence|Repositories|Config)(\.|$)")
            .Should().NotHaveDependencyOnAny($"{Root}.Persistence.AppDbContext", "Microsoft.EntityFrameworkCore.DbContext")
            .GetResult().FailingTypeNames ?? [];

        Assert.Empty(offenders.Where(name => !name.Contains('<')
                                             && name != $"{Root}.Common.BaseRepository`1"
                                             && name != "Program"));
    }

    [Fact]
    public void EveryTypeLivesInAKnownFolder()
    {
        var offenders = Api.GetTypes()
            .Where(type => !type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute))
                           && type.Name != "Program"
                           && !type.FullName!.StartsWith("Microsoft.", StringComparison.Ordinal)
                           && !type.FullName.StartsWith("System.", StringComparison.Ordinal)
                           && !type.FullName.Contains('<'))
            .Where(type => type.Namespace?.Split('.') is not [_, "Api", var folder, ..] || !TopLevelFolders.Contains(folder))
            .Select(type => type.FullName)
            .ToList();

        Assert.Empty(offenders);
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

    /// <summary>Controllers are checked by <see cref="ControllersSitDirectlyInTheirTopLevelModuleFolder"/> instead: they keep
    /// only the top-level module folder, while the other layers follow the full module tree. In Services and Repositories a
    /// module folder may hold one more level: the folder of one interface and its class (<c>Auth/Login</c>).</summary>
    [Fact]
    public void LayerNamespacesFollowTheModuleTree()
    {
        var offenders = LayerFolders.Where(layer => layer != "Controllers")
            .SelectMany(layer => TypesIn(layer).Select(type => (layer, type)))
            .Where(item => ModulePathOf(item.type.Namespace!, item.layer) is var path
                           && !IsAllowedModulePath(path)
                           && !(item.layer is "Services" or "Repositories" && IsAllowedModulePath(ParentPath(path))))
            .Select(item => item.type.FullName)
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>A folder holds either code or sub-folders, never both, so nothing sits loose beside a sub-module. A module's
    /// shared <c>Rules</c> folder and non-code folders (the assistant's help topics) are the exception.</summary>
    [Fact]
    public void FoldersHoldEitherCodeOrSubFolders()
    {
        var roots = new[] { "Services", "Repositories", "Integrations" }.Select(layer => Path.Combine(SourceRoot, layer))
            .Concat(TestProjectRoots.SelectMany(Directory.EnumerateDirectories).Where(directory => !IsTestBuildOutput(directory)));
        var offenders = roots
            .SelectMany(root => Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).Prepend(root))
            .Where(directory => !IsBuildOutput(directory) && !IsTestBuildOutput(directory))
            .Where(directory => Directory.EnumerateFiles(directory, "*.cs").Any()
                                && Directory.EnumerateDirectories(directory)
                                    .Any(child => Path.GetFileName(child) != "Rules" && Directory.EnumerateFiles(child, "*.cs", SearchOption.AllDirectories).Any()))
            .Select(directory => Path.GetRelativePath(Path.GetDirectoryName(SourceRoot)!, directory))
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>Every service and every repository has a folder of its own: its interface, its class and the helpers only
    /// it uses.</summary>
    [Fact]
    public void EachServiceAndRepositoryHasAFolderOfItsOwn()
    {
        var offenders = TypesIn("Services").Concat(TypesIn("Repositories"))
            .Where(type => type.IsInterface && type.IsPublic
                           && (type.Name.EndsWith("Service", StringComparison.Ordinal)
                               || type.Name.EndsWith("Repository", StringComparison.Ordinal)
                               || type.Name.EndsWith("Repository`1", StringComparison.Ordinal)))
            .GroupBy(type => type.Namespace)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} ({string.Join(", ", group.Select(type => type.Name))})")
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void ControllersSitDirectlyInTheirTopLevelModuleFolder()
    {
        var modules = ModulePaths.Select(path => path.Split('.')[0]).ToHashSet();
        var offenders = TypesIn("Controllers")
            .Where(type => ModulePathOf(type.Namespace!, "Controllers") is var path && !modules.Contains(path))
            .Select(type => type.FullName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void NoSeparateInterfacesFolder()
    {
        var offenders = Directory.EnumerateDirectories(SourceRoot, "Interfaces", SearchOption.AllDirectories)
            .Where(directory => !IsBuildOutput(directory))
            .Select(directory => Path.GetRelativePath(SourceRoot, directory))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void EveryInterfaceHasAFileOfItsOwn()
    {
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(SourceRoot, "*.cs", SearchOption.AllDirectories).Where(file => !IsBuildOutput(file)))
        {
            var interfaces = Regex.Matches(StripComments(File.ReadAllText(file)), @"^(?:public|internal)\s+(?:partial\s+)?interface\s+(\w+)", RegexOptions.Multiline)
                .Select(match => match.Groups[1].Value)
                .ToList();
            if (interfaces.Count > 1 || (interfaces.Count == 1 && Path.GetFileNameWithoutExtension(file) != interfaces[0]))
            {
                offenders.Add($"{Path.GetRelativePath(SourceRoot, file)} ({string.Join(", ", interfaces)})");
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void EveryInterfaceSitsBesideItsImplementation()
    {
        var types = Api.GetTypes().Where(type => !type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute))).ToList();
        var offenders = types
            .Where(type => type.IsInterface && type.Namespace?.StartsWith($"{Root}.", StringComparison.Ordinal) == true)
            .SelectMany(contract => types
                .Where(type => type is { IsClass: true, IsAbstract: false } && type.GetInterfaces().Contains(contract)
                               && TopFolderOf(type) == TopFolderOf(contract) && type.Namespace != contract.Namespace)
                .Select(type => $"{contract.FullName} / {type.FullName}"))
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

        Assert.Empty(offenders.Where(name => !name.Contains('<')));
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
        return Regex.Replace(path, @"\.(Rules|Validators)$", string.Empty);
    }

    private static string? TopFolderOf(Type type) => type.Namespace?.Split('.') is [_, "Api", var folder, ..] ? folder : null;

    private static bool IsAllowedModulePath(string path) => path.Length == 0 || ModulePaths.Contains(path);

    private static string ParentPath(string path) => path.Contains('.') ? path[..path.LastIndexOf('.')] : string.Empty;

    /// <summary>The unit and integration test projects, beside the API under the solution folder.</summary>
    private static string[] TestProjectRoots =>
        new[] { "TaskFlow.UnitTests", "TaskFlow.IntegrationTests" }.Select(name => Path.Combine(SourceRoot, "..", "..", "tests", name)).ToArray();

    private static bool IsTestBuildOutput(string path) => Path.GetFileName(path) is "bin" or "obj" or "Properties"
                                                          || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                                                          || path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}");

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
