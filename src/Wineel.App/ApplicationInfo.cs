using System.Reflection;

namespace Wineel;

internal static class ApplicationInfo
{
    public static string Version
    {
        get
        {
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informational)) return informational.Split('+', 2)[0];
            return assembly.GetName().Version?.ToString(3) ?? "unknown";
        }
    }
}
