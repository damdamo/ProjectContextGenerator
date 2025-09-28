using ProjectContextGenerator.Domain.Abstractions;

namespace ProjectContextGenerator.Tests.Fakes
{
    public sealed class FakeProcessRunner : IProcessRunner
    {
        public sealed record Invocation(string FileName, string Arguments, string WorkingDirectory, TimeSpan? Timeout);

        // Scripted responses in FIFO order
        private readonly Queue<ProcessResult> _script = new();
        public Invocation? LastInvocation { get; private set; }

        /// <summary>Add a scripted result to be returned on next Run().</summary>
        public void EnqueueResult(int exitCode, string stdout = "", string stderr = "")
        {
            _script.Enqueue(new ProcessResult
            {
                ExitCode = exitCode,
                StdOut = stdout,
                StdErr = stderr
            });
        }

        /// <summary>Clear any scripted results.</summary>
        public void Reset() => _script.Clear();

        public ProcessResult Run(string fileName, string arguments, string workingDirectory, TimeSpan? timeout = null)
        {
            LastInvocation = new Invocation(fileName, arguments, workingDirectory, timeout);
            return _script.Count > 0
                ? _script.Dequeue()
                : new ProcessResult { ExitCode = 0, StdOut = "", StdErr = "" };
        }
    }
}
