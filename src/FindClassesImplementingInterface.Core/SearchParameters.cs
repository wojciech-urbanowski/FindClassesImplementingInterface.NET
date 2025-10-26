using System.Text.Json;

namespace FindClassesImplementingInterface.Core;

public sealed record SearchParameters(
    string SearchFolder,
    string FileSearchPattern,
    string InterfaceName)
{
    public static SearchParameters FromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Parameter file not found: {filePath}");
        }

        using var stream = File.OpenRead(filePath);
        var parameters = JsonSerializer.Deserialize<SearchParameters>(stream, JsonOptions);
        if (parameters is null)
        {
            throw new InvalidOperationException("Unable to read input parameters.");
        }

        return new SearchParameters(
            NormalizeDirectory(parameters.SearchFolder),
            RequireValue(parameters.FileSearchPattern, nameof(FileSearchPattern)),
            RequireValue(parameters.InterfaceName, nameof(InterfaceName)));
    }

    private static string NormalizeDirectory(string? path)
    {
        var value = RequireValue(path, nameof(SearchFolder));
        return Path.GetFullPath(value);
    }

    private static string RequireValue(string? value, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"The parameter {propertyName} cannot be empty.");
        }

        return value.Trim();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}
