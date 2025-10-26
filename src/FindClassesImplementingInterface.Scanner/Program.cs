using System.Reflection;
using System.Runtime.InteropServices;
using FindClassesImplementingInterface.Core;

if (args.Length < 2)
{
    Console.Error.WriteLine("Invalid arguments supplied to the worker process.");
    return 1;
}

var assemblyPath = args[0];
var interfaceName = args[1];

if (!File.Exists(assemblyPath))
{
    Console.Error.WriteLine($"Assembly file not found: {assemblyPath}.");
    return 2;
}

try
{
    foreach (var typeName in FindImplementations(assemblyPath, interfaceName))
    {
        Console.WriteLine(typeName);
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 3;
}

static IEnumerable<string> FindImplementations(string assemblyPath, string interfaceFullName)
{
    var resolver = CreateResolver(assemblyPath);
    using var context = new MetadataLoadContext(resolver);

    var assembly = context.LoadFromAssemblyPath(assemblyPath);
    LoadReferencedAssemblies(context, assembly);

    var interfaceType = ResolveInterface(context, interfaceFullName);
    if (interfaceType is null)
    {
        throw new InvalidOperationException($"Unable to locate interface {interfaceFullName}.");
    }

    foreach (var type in assembly.GetTypes())
    {
        if (type.IsInterface)
        {
            continue;
        }

        if (type == interfaceType)
        {
            continue;
        }

        if (ImplementsInterface(type, interfaceType))
        {
            yield return type.FullName ?? type.Name;
        }
    }
}

static void LoadReferencedAssemblies(MetadataLoadContext context, Assembly assembly)
{
    foreach (var reference in assembly.GetReferencedAssemblies())
    {
        try
        {
            context.LoadFromAssemblyName(reference);
        }
        catch
        {
            // Ignore assemblies that cannot be resolved in the current context.
        }
    }
}

static bool ImplementsInterface(Type candidate, Type interfaceType)
{
    if (interfaceType.IsAssignableFrom(candidate))
    {
        return true;
    }

    if (candidate.IsGenericType && !candidate.IsGenericTypeDefinition)
    {
        return interfaceType.IsAssignableFrom(candidate.GetGenericTypeDefinition());
    }

    return false;
}

static PathAssemblyResolver CreateResolver(string assemblyPath)
{
    var assemblyDirectory = Path.GetDirectoryName(assemblyPath);
    var runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();

    var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    if (!string.IsNullOrEmpty(runtimeDirectory) && Directory.Exists(runtimeDirectory))
    {
        foreach (var file in Directory.EnumerateFiles(runtimeDirectory, "*.dll"))
        {
            paths.Add(file);
        }
    }

    if (!string.IsNullOrEmpty(assemblyDirectory) && Directory.Exists(assemblyDirectory))
    {
        foreach (var file in Directory.EnumerateFiles(assemblyDirectory, "*.dll"))
        {
            paths.Add(file);
        }

        foreach (var file in Directory.EnumerateFiles(assemblyDirectory, "*.exe"))
        {
            paths.Add(file);
        }
    }

    paths.Add(assemblyPath);

    return new PathAssemblyResolver(paths);
}

static Type? ResolveInterface(MetadataLoadContext context, string interfaceFullName)
{
    foreach (var assembly in context.GetAssemblies())
    {
        var type = assembly.GetType(interfaceFullName, throwOnError: false);
        if (type is not null)
        {
            return type;
        }
    }

    return null;
}
