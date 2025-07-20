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
        
        /// <summary>Size of the cache location in bytes.</summary>
        public long Size { get; set; }
        
        /// <summary>Size in megabytes.</summary>
        public double SizeMB => Size / (1024.0 * 1024.0);
    }
    
    /// <summary>
    /// Result of a cache cleaning operation.
    /// </summary>
    public sealed class CleanResult {
        /// <summary>Locations that were successfully cleaned.</summary>
        public List<CacheLocation> SuccessfullyCleared { get; set; } = new List<CacheLocation>();
        
        /// <summary>Locations that failed to clean with error messages.</summary>
        public List<(CacheLocation Location, string Error)> Failed { get; set; } = new List<(CacheLocation, string)>();
        
        /// <summary>Total size cleared in bytes.</summary>
        public long TotalSizeCleared => SuccessfullyCleared.Sum(l => l.Size);
        
        /// <summary>Total size cleared in megabytes.</summary>
        public double TotalSizeClearedMB => TotalSizeCleared / (1024.0 * 1024.0);
        
        /// <summary>Whether all locations were cleaned successfully.</summary>
        public bool Success => Failed.Count == 0;
    }
    
    /// <summary>
    /// Gets all Playwright cache locations.
    /// </summary>
    /// <param name="includeBrowsers">Include browser download locations.</param>
    /// <param name="includeTemp">Include temporary files.</param>
    /// <returns>List of cache locations found.</returns>
    public static List<CacheLocation> GetCacheLocations(bool includeBrowsers = true, bool includeTemp = true) {
        var locations = new List<CacheLocation>();
        
        if (includeBrowsers) {
            // Playwright cache in LocalAppData
            var playwrightPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ms-playwright"
            );
            
            if (Directory.Exists(playwrightPath)) {
                locations.Add(new CacheLocation {
                    Path = playwrightPath,
                    Description = "Playwright browsers",
                    Size = GetDirectorySize(playwrightPath)
                });
            }
            
            // User profile .cache directory
            var cachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".cache", "ms-playwright"
            );
            
            if (Directory.Exists(cachePath)) {
                locations.Add(new CacheLocation {
                    Path = cachePath,
                    Description = "Playwright cache",
                    Size = GetDirectorySize(cachePath)
                });
            }
        }
        
        if (includeTemp) {
            var tempPath = Path.GetTempPath();
            
            // Playwright temp directories
            try {
                var playwrightTempDirs = Directory.GetDirectories(tempPath, "playwright*", SearchOption.TopDirectoryOnly);
                foreach (var dir in playwrightTempDirs) {
                    locations.Add(new CacheLocation {
                        Path = dir,
                        Description = "Playwright temp files",
                        Size = GetDirectorySize(dir)
                    });
                }
            } catch {
                // Ignore access errors
            }
            
            // Trace files
            try {
                var traceDirs = Directory.GetDirectories(tempPath, "trace*", SearchOption.TopDirectoryOnly);
                foreach (var dir in traceDirs) {
                    if (Directory.GetFiles(dir, "*.trace").Length > 0) {
                        locations.Add(new CacheLocation {
                            Path = dir,
                            Description = "Trace files",
                            Size = GetDirectorySize(dir)
                        });
                    }
                }
            } catch {
                // Ignore access errors
            }
        }
        
        return locations;
    }
    
    /// <summary>
    /// Cleans the specified cache locations.
    /// </summary>
    /// <param name="locations">Locations to clean.</param>
    /// <returns>Result of the cleaning operation.</returns>
    public static CleanResult CleanCache(IEnumerable<CacheLocation> locations) {
        var result = new CleanResult();
        
        foreach (var location in locations) {
            try {
                Directory.Delete(location.Path, recursive: true);
                result.SuccessfullyCleared.Add(location);
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
        try {
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories)) {
                try {
                    size += new FileInfo(file).Length;
                } catch {
                    // Ignore individual file access errors
                }
            }
        } catch {
            // Ignore directory access errors
        }
        return size;
    }
}