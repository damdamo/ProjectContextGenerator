namespace ProjectContextGenerator.Domain.Options
{
    public sealed record TreeScanOptions(
        int MaxDepth = 4,
        IReadOnlyList<string>? IncludeGlobs = null,   // ex: ["**/*.cs", "**/*.csproj"]
        IReadOnlyList<string>? ExcludeGlobs = null,   // ex: ["**/bin/**","**/obj/**","**/node_modules/**"]
        bool SortDirectoriesFirst = true,
        bool CollapseSingleChildDirectories = true,
        int? MaxItemsPerDirectory = null
    );
}