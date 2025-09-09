namespace ProjectContextGenerator.Domain.Abstractions
{
    public interface IPathMatcher
    {
        // Return true if this path (relative to the scan root) should be INCLUDED
        bool IsMatch(string relativePath, bool isDirectory);
    }
}
