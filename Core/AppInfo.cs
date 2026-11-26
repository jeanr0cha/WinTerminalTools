using System.Reflection;

namespace WinTerminalTools.Core;

/// <summary>Dados do app lidos do assembly. A versão tem uma fonte só: &lt;Version&gt; no .csproj.</summary>
public static class AppInfo
{
    public static string Version { get; } = ResolveVersion(typeof(AppInfo).Assembly);

    /// <summary>"2.0.0" a partir do InformationalVersion (que pode vir como "2.0.0+commit").</summary>
    public static string ResolveVersion(Assembly assembly)
    {
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plus = informational.IndexOf('+');
            return plus >= 0 ? informational[..plus] : informational;
        }

        var version = assembly.GetName().Version;
        return version == null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
