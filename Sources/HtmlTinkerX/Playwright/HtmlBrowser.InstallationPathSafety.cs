using System;
using System.IO;
using System.Linq;

namespace HtmlTinkerX;

public static partial class HtmlBrowser {
    /// <summary>
    /// Validates a driver destination before mutation. Mutable drivers require a direct local destination
    /// outside package caches, package payloads, and checkout roots.
    /// Dedicated application directories beneath a checkout remain valid.
    /// </summary>
    internal static void ValidateDriverMutationPath(string driverPath) {
        driverPath = Path.GetFullPath(driverPath);
        ValidateInstallationDestination(driverPath, Path.GetDirectoryName(driverPath)!, "PLAYWRIGHT_DRIVER_SEARCH_PATH");
    }

    private static string ValidateInstallationMutationPaths() {
        string driverPath = GetDriverPath();
        // A complete bundled driver is reused read-only; an incomplete bundle must pass the mutation guard.
        if (!IsReadOnlyBundledDriverPath(driverPath)) ValidateDriverMutationPath(driverPath);
        return ValidateBrowserMutationPath(driverPath);
    }

    private static string ValidateBrowserMutationPath(string? driverPath = null) =>
        ValidateDirectoryMutationPath(GetBrowserInstallPath(driverPath), "PLAYWRIGHT_BROWSERS_PATH");

    /// <summary>Applies the installation safety contract to a public cache-cleaner destination.</summary>
    internal static string ValidateCacheMutationPath(string path) =>
        ValidateDirectoryMutationPath(path, "CacheLocation.Path");

    private static string ValidateDirectoryMutationPath(string path, string setting) {
        path = Path.GetFullPath(path);
        ValidateInstallationDestination(path, path, setting);
        if (Directory.Exists(path)) {
            // Repair/clean can remove the whole root; reject a protected child before either installation changes.
            foreach (string child in Directory.GetDirectories(path)) {
                ValidateInstallationDestination(child, child, setting);
            }
        }
        return path;
    }

    /// <summary>Checks the actual mutation target separately from its configured search parent.</summary>
    private static void ValidateInstallationDestination(string path, string searchRoot, string setting) {
        string? configuredPackages = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        string defaultPackages = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        string[] packageRoots = string.IsNullOrWhiteSpace(configuredPackages)
            ? new[] { defaultPackages } : new[] { defaultPackages, configuredPackages! };
        foreach (string packageRoot in packageRoots) {
            if (InstallationPathsOverlap(path, packageRoot)) {
                throw ProtectedInstallationDestination(searchRoot, setting, "a NuGet package cache");
            }
        }

        string identity = HtmlBrowserFileSystemPath.GetValidatedInstallationPathIdentity(path);
        foreach (string packageRoot in packageRoots) {
            if (!HtmlBrowserFileSystemPath.IsNetworkOrDevicePath(packageRoot)
                && InstallationPathsOverlap(identity, HtmlBrowserFileSystemPath.GetPathIdentity(packageRoot))) {
                throw ProtectedInstallationDestination(searchRoot, setting, "a NuGet package cache");
            }
        }

        // Package metadata also identifies RestorePackagesPath/custom-cache payloads without relying on environment routing.
        for (DirectoryInfo? directory = new DirectoryInfo(path); directory is not null; directory = directory.Parent) {
            if (File.Exists(Path.Combine(directory.FullName, ".nupkg.metadata"))
                || (directory.Exists && directory.EnumerateFiles("*.nuspec").Any()
                    && directory.EnumerateFiles("*.nupkg.sha512").Any())) {
                throw ProtectedInstallationDestination(searchRoot, setting, "a NuGet package payload");
            }
        }

        foreach (string directory in new[] { searchRoot, path }) {
            string git = Path.Combine(directory, ".git");
            if (Directory.Exists(git) || File.Exists(git)) {
                throw ProtectedInstallationDestination(searchRoot, setting, "a source checkout root");
            }
        }
    }

    private static bool InstallationPathsOverlap(string path, string protectedPath) =>
        IsWithinInstallationPath(path, protectedPath) || IsWithinInstallationPath(protectedPath, path);

    private static bool IsWithinInstallationPath(string path, string protectedPath) {
        string root = Path.GetFullPath(protectedPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string candidate = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(candidate, root, FileSystemPathComparison)
            || candidate.StartsWith(root + Path.DirectorySeparatorChar, FileSystemPathComparison);
    }

    private static InvalidOperationException ProtectedInstallationDestination(string root, string setting, string kind) => new(
        $"The Playwright installation destination '{root}' is protected because it is {kind}. "
        + $"Set {setting} to a dedicated local directory before installing, repairing, or cleaning.");
}
