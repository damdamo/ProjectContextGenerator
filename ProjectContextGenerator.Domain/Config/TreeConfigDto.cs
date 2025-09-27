namespace ProjectContextGenerator.Domain.Config
{
    /// <summary>
    /// Represents the raw configuration options loaded from JSON before normalization.
    /// Nullable properties indicate optional values that may fall back to defaults.
    /// </summary>
    public sealed class TreeConfigDto
    {
        public int? Version { get; init; }
        public string? Root { get; init; }
        public int? MaxDepth { get; init; }
        public IReadOnlyList<string>? Include { get; init; }
        public IReadOnlyList<string>? Exclude { get; init; }
        public string? GitIgnore { get; init; }
        public string? GitIgnoreFileName { get; init; }
        public bool? SortDirectoriesFirst { get; init; }
        public bool? CollapseSingleChildDirectories { get; init; }
        public int? MaxItemsPerDirectory { get; init; }
        public bool? DirectoriesOnly { get; init; }

        /// <summary>
        /// Profiles allow defining named presets. Each profile is a partial configuration
        /// that overrides values from the root configuration when selected.
        /// </summary>
        public IReadOnlyDictionary<string, TreeConfigDto>? Profiles { get; init; }
    }
}
