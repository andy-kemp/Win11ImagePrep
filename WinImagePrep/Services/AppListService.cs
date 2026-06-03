using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinImagePrep.Models;

namespace WinImagePrep.Services
{
    public class AppListService
    {
        // GitHub raw URL for the app list (can be updated to point to your repo)
        private const string DefaultAppListUrl = "https://raw.githubusercontent.com/andy-kemp/Win11ImagePrep/main/app-list.json";

        private readonly string _cacheDirectory;
        private readonly string _cacheFilePath;
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        public AppListService()
        {
            _cacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Win11ImagePrep",
                "Cache");
            _cacheFilePath = Path.Combine(_cacheDirectory, "app-list.json");

            // Ensure cache directory exists
            Directory.CreateDirectory(_cacheDirectory);
        }

        /// <summary>
        /// Load app list with fallback chain:
        /// 1. Try GitHub (with timeout)
        /// 2. Try local cache
        /// 3. Return empty list
        /// </summary>
        public async Task<List<WindowsApp>> LoadAppListAsync(
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            // Try GitHub first
            try
            {
                progress?.Report("Downloading latest app list from GitHub...");
                var apps = await DownloadFromGitHubAsync(cancellationToken);

                if (apps != null && apps.Any())
                {
                    progress?.Report($"✓ Downloaded {apps.Count} apps from GitHub");

                    // Save to cache for offline use
                    await SaveToCacheAsync(apps);
                    return apps;
                }
            }
            catch (TaskCanceledException)
            {
                progress?.Report("⚠ GitHub download timed out, trying cache...");
            }
            catch (HttpRequestException ex)
            {
                progress?.Report($"⚠ GitHub download failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                progress?.Report($"⚠ Error downloading from GitHub: {ex.Message}");
            }

            // Fallback to cache
            try
            {
                progress?.Report("Loading app list from cache...");
                var cachedApps = await LoadFromCacheAsync();

                if (cachedApps != null && cachedApps.Any())
                {
                    progress?.Report($"✓ Loaded {cachedApps.Count} apps from cache");
                    return cachedApps;
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"⚠ Cache load failed: {ex.Message}");
            }

            progress?.Report("⚠ No app list available - please scan from ISO");
            return new List<WindowsApp>();
        }

        /// <summary>
        /// Download app list from GitHub
        /// </summary>
        private async Task<List<WindowsApp>?> DownloadFromGitHubAsync(CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync(DefaultAppListUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var appDtos = JsonSerializer.Deserialize<List<WindowsAppDto>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return appDtos?.Select(dto => new WindowsApp
            {
                PackageName = dto.PackageName ?? string.Empty,
                DisplayName = dto.DisplayName ?? string.Empty,
                Description = dto.Description ?? string.Empty,
                IsSelected = false
            }).ToList();
        }

        /// <summary>
        /// Load app list from local cache
        /// </summary>
        private async Task<List<WindowsApp>?> LoadFromCacheAsync()
        {
            if (!File.Exists(_cacheFilePath))
                return null;

            var json = await File.ReadAllTextAsync(_cacheFilePath);
            var appDtos = JsonSerializer.Deserialize<List<WindowsAppDto>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return appDtos?.Select(dto => new WindowsApp
            {
                PackageName = dto.PackageName ?? string.Empty,
                DisplayName = dto.DisplayName ?? string.Empty,
                Description = dto.Description ?? string.Empty,
                IsSelected = false
            }).ToList();
        }

        /// <summary>
        /// Save app list to local cache
        /// </summary>
        private async Task SaveToCacheAsync(List<WindowsApp> apps)
        {
            var appDtos = apps.Select(app => new WindowsAppDto
            {
                PackageName = app.PackageName,
                DisplayName = app.DisplayName,
                Description = app.Description
            }).ToList();

            var json = JsonSerializer.Serialize(appDtos, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_cacheFilePath, json);
        }

        /// <summary>
        /// Save scanned apps to cache (after manual ISO scan)
        /// </summary>
        public async Task SaveScannedAppsAsync(List<WindowsApp> apps)
        {
            await SaveToCacheAsync(apps);
        }

        /// <summary>
        /// Clear the local cache
        /// </summary>
        public void ClearCache()
        {
            if (File.Exists(_cacheFilePath))
            {
                File.Delete(_cacheFilePath);
            }
        }
    }

    // DTO for JSON serialization
    internal class WindowsAppDto
    {
        public string? PackageName { get; set; }
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
    }
}
