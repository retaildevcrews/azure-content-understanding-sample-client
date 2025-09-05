using System.IO;

namespace ContentUnderstanding.Client.Utilities
{
    internal static class PathResolver
    {
    public static string ProjectRoot() => Directory.GetCurrentDirectory();
    public static string DataDir() => Path.Combine(ProjectRoot(), "Data");
    public static string OutputDir() => Path.Combine(ProjectRoot(), "Output");
    public static string SampleDocumentsDir() => Path.Combine(DataDir(), "SampleDocuments");
    }
}
