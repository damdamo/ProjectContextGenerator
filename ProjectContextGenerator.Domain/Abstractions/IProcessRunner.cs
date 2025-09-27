namespace ProjectContextGenerator.Domain.Abstractions
{
    public sealed class ProcessResult
    {
        public int ExitCode { get; init; }
        public string StdOut { get; init; } = string.Empty;
        public string StdErr { get; init; } = string.Empty;
    }

    public interface IProcessRunner
    {
        ProcessResult Run(
        string fileName,
        string arguments,
        string workingDirectory,
        TimeSpan? timeout = null);
    }
}
