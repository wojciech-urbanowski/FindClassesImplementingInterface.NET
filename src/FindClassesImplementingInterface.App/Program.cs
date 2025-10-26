using System.Diagnostics;
using System.Text;
using FindClassesImplementingInterface.Core;

var baseDirectory = AppContext.BaseDirectory;
var parameterFilePath = Path.Combine(baseDirectory, "FindClassesImplementingInterface.NET.parameters.json");

SearchParameters parameters;
try
{
    parameters = SearchParameters.FromFile(parameterFilePath);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to read parameters: {ex.Message}");
    return;
}

var workerCommand = LocateWorkerCommand(baseDirectory);
if (workerCommand is null)
{
    Console.Error.WriteLine("The worker process responsible for scanning assemblies could not be located.");
    return;
}

var results = new List<SearchResult>();
IEnumerable<string> candidateFiles;
try
{
    candidateFiles = Directory.EnumerateFiles(parameters.SearchFolder, parameters.FileSearchPattern, SearchOption.AllDirectories);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to enumerate files under {parameters.SearchFolder}: {ex.Message}");
    return;
}

foreach (var filePath in candidateFiles)
{
    if (!IsSupportedAssembly(filePath))
    {
        continue;
    }

    var (exitCode, output, errorOutput) = ExecuteWorker(workerCommand, filePath, parameters.InterfaceName);
    if (exitCode != 0)
    {
        var trimmedError = errorOutput.Trim();
        var message = string.IsNullOrEmpty(trimmedError)
            ? $"Failed to analyze {filePath}."
            : $"Failed to analyze {filePath}: {trimmedError}";
        Console.Error.WriteLine(message);
        continue;
    }

    if (string.IsNullOrWhiteSpace(output))
    {
        continue;
    }

    var relativePath = Path.GetRelativePath(parameters.SearchFolder, filePath);
    using var reader = new StringReader(output);
    string? line;
    while ((line = reader.ReadLine()) is not null)
    {
        line = line.Trim();
        if (line.Length == 0)
        {
            continue;
        }

        results.Add(new SearchResult(relativePath, line));
    }
}

var outputFilePath = Path.Combine(baseDirectory, "FindClassesImplementingInterface.NET.output.json");
try
{
    var orderedResults = results
        .OrderBy(r => r.FileName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(r => r.TypeName, StringComparer.Ordinal)
        .ToList();
    ResultWriter.WriteResults(outputFilePath, orderedResults);
    Console.WriteLine($"Results saved to {outputFilePath}.");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to write results: {ex.Message}");
}

static bool IsSupportedAssembly(string filePath)
{
    var extension = Path.GetExtension(filePath);
    return extension.Equals(".dll", StringComparison.OrdinalIgnoreCase)
           || extension.Equals(".exe", StringComparison.OrdinalIgnoreCase);
}

static (int ExitCode, string StdOut, string StdErr) ExecuteWorker(WorkerCommand workerCommand, string assemblyPath, string interfaceName)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = workerCommand.FileName,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8
    };

    foreach (var argument in workerCommand.Arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    startInfo.ArgumentList.Add(assemblyPath);
    startInfo.ArgumentList.Add(interfaceName);

    using var process = Process.Start(startInfo);
    if (process is null)
    {
        return (-1, string.Empty, "Unable to start worker process.");
    }

    var output = process.StandardOutput.ReadToEnd();
    var error = process.StandardError.ReadToEnd();
    process.WaitForExit();

    return (process.ExitCode, output, error);
}

static WorkerCommand? LocateWorkerCommand(string baseDirectory)
{
    var workerExe = Path.Combine(baseDirectory, "FindClassesImplementingInterface.Scanner.exe");
    if (File.Exists(workerExe))
    {
        return new WorkerCommand(workerExe, Array.Empty<string>());
    }

    var workerBinary = Path.Combine(baseDirectory, "FindClassesImplementingInterface.Scanner");
    if (File.Exists(workerBinary))
    {
        return new WorkerCommand(workerBinary, Array.Empty<string>());
    }

    var workerDll = Path.Combine(baseDirectory, "FindClassesImplementingInterface.Scanner.dll");
    if (File.Exists(workerDll))
    {
        return new WorkerCommand("dotnet", new[] { workerDll });
    }

    return null;
}

internal sealed record WorkerCommand(string FileName, IReadOnlyList<string> Arguments);
