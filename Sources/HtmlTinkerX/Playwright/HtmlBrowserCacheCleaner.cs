using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HtmlTinkerX;

/// <summary>
/// Provides methods for cleaning Playwright browser cache and temporary files.
/// </summary>
public static class HtmlBrowserCacheCleaner {
    /// <summary>
    /// Represents a cache location to clean.
    /// </summary>
    public sealed class CacheLocation {
        /// <summary>Path to the cache location.</summary>
        public string Path { get; set; } = string.Empty;
        
        /// <summary>Description of what this location contains.</summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>Estimated size of the cache location in bytes.</summary>
        public long Size { get; set; }
        
        /// <summary>Size in megabytes.</summary>
        public double SizeMB => Size / (1024.0 * 1024.0);
    }
    
    /// <summary>
    /// Result of a cache cleaning operation.
    /// </summary>
    public sealed class CleanResult {
        private readonly Dictionary<CacheLocation, string> _clearedIdentities = new Dictionary<CacheLocation, string>();

        /// <summary>Locations that were successfully cleaned.</summary>
        public List<CacheLocation> SuccessfullyCleared { get; set; } = new List<CacheLocation>();
        
        /// <summary>Locations that failed to clean with error messages.</summary>
        public List<(CacheLocation Location, string Error)> Failed { get; set; } = new List<(CacheLocation, string)>();
        
        /// <summary>Estimated bytes cleared, counting each outermost successful footprint once.</summary>
        public long TotalSizeCleared => GetEstimatedFootprintSize(SuccessfullyCleared, _clearedIdentities);
        
        /// <summary>Total size cleared in megabytes.</summary>
        public double TotalSizeClearedMB => TotalSizeCleared / (1024.0 * 1024.0);
        
        /// <summary>Whether all locations were cleaned successfully.</summary>
        public bool Success => Failed.Count == 0;

        /// <summary>Retains the pre-delete identity for accounting without changing the supplied location or estimate.</summary>
        internal void RecordCleared(CacheLocation location, string identity) {
            _clearedIdentities[location] = identity;
            SuccessfullyCleared.Add(location);
        }
    }

    /// <summary>
    /// Estimates the total bytes represented by the supplied locations, counting outermost physical footprints once.
    /// Uses the supplied sizes; the first estimate wins for duplicate identities. Path inspection is read-only
    /// and does not authorize cleanup.
    /// </summary>
    /// <param name="locations">Cache locations and their size estimates.</param>
    /// <returns>The estimated bytes without overlapping descendant or duplicate footprints.</returns>
    /// <exception cref="ArgumentNullException">The locations collection is null.</exception>
    /// <exception cref="ArgumentException">A location is null.</exception>
    public static long GetEstimatedTotalSize(IEnumerable<CacheLocation> locations) {
        if (locations is null) throw new ArgumentNullException(nameof(locations));
        var entries = locations.ToList();
        var identities = new Dictionary<CacheLocation, string>();
        foreach (CacheLocation location in entries) {
            if (location is null) throw new ArgumentException("A cache location cannot be null.", nameof(locations));
            identities[location] = HtmlBrowserFileSystemPath.GetPathIdentity(location.Path);
        }
        return GetEstimatedFootprintSize(entries, identities);
    }

    private static long GetEstimatedFootprintSize(IReadOnlyList<CacheLocation> locations, IReadOnlyDictionary<CacheLocation, string> identities) {
        StringComparison comparison = Environment.OSVersion.Platform == PlatformID.Win32NT
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var counted = new HashSet<string>(Environment.OSVersion.Platform == PlatformID.Win32NT
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        long total = 0;
        foreach (CacheLocation location in locations) {
            // Caller-added result entries retain their original estimate behavior without filesystem inspection.
            if (!identities.TryGetValue(location, out string? identity)) {
                total += location.Size;
                continue;
            }
            if (!counted.Add(identity)) continue;
            bool hasAncestor = locations.Any(other => !ReferenceEquals(location, other)
                && identities.TryGetValue(other, out string? parent)
                && identity.StartsWith(parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar, comparison));
            if (!hasAncestor) total += location.Size;
        }
        return total;
    }
    
    /// <summary>
    /// Gets all Playwright cache locations.
    /// </summary>
    /// <param name="includeBrowsers">Include browser download locations.</param>
    /// <param name="includeTemp">Include temporary files.</param>
    /// <returns>List of cache locations found.</returns>
    public static List<CacheLocation> GetCacheLocations(bool includeBrowsers = true, bool includeTemp = true) {
        var locations = new List<CacheLocation>();
        var identities = new HashSet<string>(Environment.OSVersion.Platform == PlatformID.Win32NT
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        
        if (includeBrowsers) {
            foreach (string browserPath in HtmlBrowser.GetBrowserCachePaths()) {
                AddCacheLocation(locations, identities, browserPath, "Playwright browsers");
            }

            foreach (string driverPath in HtmlBrowser.GetMutableDriverCachePaths()) {
                AddCacheLocation(locations, identities, driverPath, "Playwright driver and Node.js");
            }
        }
        
        if (includeTemp) {
            var tempPath = Path.GetTempPath();
            
            // Playwright temp directories
            try {
                var playwrightTempDirs = Directory.GetDirectories(tempPath, "playwright*", SearchOption.TopDirectoryOnly);
                foreach (var dir in playwrightTempDirs) {
                    AddCacheLocation(locations, identities, dir, "Playwright temp files");
                }
            } catch {
                // Ignore access errors
            }
            
            // Trace files
            try {
                var traceDirs = Directory.GetDirectories(tempPath, "trace*", SearchOption.TopDirectoryOnly);
                foreach (var dir in traceDirs) {
                    if (Directory.GetFiles(dir, "*.trace").Length > 0) {
                        AddCacheLocation(locations, identities, dir, "Trace files");
                    }
                }
            } catch {
                // Ignore access errors
            }
        }
        
        return locations;
    }

    private static void AddCacheLocation(List<CacheLocation> locations, HashSet<string> identities, string path, string description) {
        path = Path.GetFullPath(path);
        string root = Path.GetPathRoot(path) ?? string.Empty;
        if (path.Length > root.Length) path = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!Directory.Exists(path)) return;
        // Physical identity deduplicates discovery only; CleanCache still authorizes the supplied path separately.
        if (!identities.Add(HtmlBrowserFileSystemPath.GetPathIdentity(path))) return;
        locations.Add(new CacheLocation { Path = path, Description = description, Size = GetDirectorySize(path) });
    }
    
    /// <summary>
    /// Cleans the specified cache locations.
    /// </summary>
    /// <param name="locations">Locations to clean.</param>
    /// <returns>Result of the cleaning operation.</returns>
    public static CleanResult CleanCache(IEnumerable<CacheLocation> locations) {
        var result = new CleanResult();
        using FileStream installationLock = HtmlBrowser.AcquireInstallationFileLockAsync().GetAwaiter().GetResult();

        foreach (var location in locations) {
            try {
                string path = HtmlBrowser.ValidateCacheMutationPath(location.Path);
                string identity = HtmlBrowserFileSystemPath.GetPathIdentity(path);
                Directory.Delete(path, recursive: true);
                result.RecordCleared(location, identity);
            } catch (Exception ex) {
                result.Failed.Add((location, ex.Message));
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Cleans all Playwright cache and temporary files.
    /// </summary>
    /// <param name="includeBrowsers">Include browser downloads.</param>
    /// <param name="includeTemp">Include temporary files.</param>
    /// <returns>Result of the cleaning operation.</returns>
    public static CleanResult CleanAllCache(bool includeBrowsers = true, bool includeTemp = true) {
        var locations = GetCacheLocations(includeBrowsers, includeTemp);
        return CleanCache(locations);
    }
    
    private static long GetDirectorySize(string path) {
        long size = 0;
        var pending = new Stack<string>();
        pending.Push(path);
        while (pending.Count > 0) {
            string directory = pending.Pop();
            try {
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
                foreach (string entry in Directory.EnumerateFileSystemEntries(directory)) {
                    try {
                        FileAttributes attributes = File.GetAttributes(entry);
                        // Match the deletion footprint without counting external targets or looping through native links.
                        if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                        if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
                        else size += new FileInfo(entry).Length;
                    } catch {
                        // Ignore individual entry access errors.
                    }
                }
            } catch {
                // Ignore directory access errors.
            }
        }
        return size;
    }
}
