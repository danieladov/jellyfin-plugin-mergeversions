using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MergeVersions
{
    public class MergeVersionsManager : IDisposable
    {
        private readonly ILibraryManager _libraryManager;
        private readonly Timer _timer;
        private readonly ILogger<MergeVersionsManager> _logger; // TODO logging
        private readonly IFileSystem _fileSystem;
        private readonly IVideoVersions _videoVersions;

        public MergeVersionsManager(
            ILibraryManager libraryManager,
            ILogger<MergeVersionsManager> logger,
            IFileSystem fileSystem,
            IVideoVersions videoVersions
        )
        {
            _libraryManager = libraryManager;
            _logger = logger;
            _fileSystem = fileSystem;
            _videoVersions = videoVersions;
            _timer = new Timer(_ => OnTimerElapsed(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public async Task MergeMoviesAsync(IProgress<double> progress, ClaimsPrincipal user = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogInformation("Scanning for repeated movies");

            var duplicateMovies = GetMoviesFromLibrary()
                .GroupBy(x => x.ProviderIds["Tmdb"])
                .Where(group => group.Count() > 1 &&
                group.Any(movie => movie.PrimaryVersionId == null &&
                                    !movie.LinkedAlternateVersions.Any()))
                .ToList();

            var current = 0;
            foreach (var movies in duplicateMovies)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var first = movies.First();
                _logger.LogInformation("Merging {Name} ({Year})", first.Name, first.ProductionYear);
                await _videoVersions.MergeAsync(movies.Select(e => e.Id).ToArray(), user, cancellationToken).ConfigureAwait(false);
                progress?.Report(++current / (double)duplicateMovies.Count * 100);
            }

            await ApplyPreferredDefaultsAsync(GetMoviesFromLibrary(), user, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(100);
        }

        public async Task SplitMoviesAsync(IProgress<double> progress, ClaimsPrincipal user = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var movies = GetMoviesFromLibrary();
            var current = 0;
            foreach (var movie in movies)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogInformation("Splitting {Name} ({Year})", movie.Name, movie.ProductionYear);
                await _videoVersions.SplitAsync(movie.Id, user, cancellationToken).ConfigureAwait(false);
                progress?.Report(++current / (double)movies.Count * 100);
            }

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(100);
        }

        public async Task MergeEpisodesAsync(IProgress<double> progress, ClaimsPrincipal user = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogInformation("Scanning for repeated episodes");

            var episodes = GetEpisodesFromLibrary();
            var duplicateEpisodes = episodes
                .GroupBy(GetEpisodeMergeKey, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1 &&
                                group.Any(episode => episode.PrimaryVersionId == null &&
                                                     !episode.LinkedAlternateVersions.Any()))
                .ToList();

            _logger.LogInformation(
                "Found {EpisodeCount} episodes and {DuplicateGroupCount} duplicate episode groups",
                episodes.Count,
                duplicateEpisodes.Count);

            var current = 0;
            foreach (var episodeGroup in duplicateEpisodes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var first = episodeGroup.First();
                _logger.LogInformation("Merging {Name} ({Year})", first.Name, first.ProductionYear);
                await _videoVersions.MergeAsync(episodeGroup.Select(e => e.Id).ToArray(), user, cancellationToken).ConfigureAwait(false);
                progress?.Report(++current / (double)duplicateEpisodes.Count * 100);
            }

            await ApplyPreferredDefaultsAsync(episodes, user, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(100);
        }

        public async Task SplitEpisodesAsync(IProgress<double> progress, ClaimsPrincipal user = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var episodes = GetEpisodesFromLibrary();
            var current = 0;

            foreach (var episode in episodes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogInformation("Splitting {EpisodeNumber} ({SeriesName})", episode.IndexNumber, episode.SeriesName);
                await _videoVersions.SplitAsync(episode.Id, user, cancellationToken).ConfigureAwait(false);
                progress?.Report(++current / (double)episodes.Count * 100);
            }

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(100);
        }

        // Jellyfin makes the widest version the default. To prefer another, split the group, give the
        // preferred version the others as alternates, and merge again: Jellyfin's merge keeps a
        // version that already has alternates as the default.
        private async Task ApplyPreferredDefaultsAsync(IEnumerable<Video> videos, ClaimsPrincipal user, CancellationToken cancellationToken)
        {
            var preferred = Plugin.Instance.PluginConfiguration.PreferredLocations ?? Array.Empty<string>();
            if (preferred.Length == 0)
            {
                return;
            }

            int Rank(Video video)
            {
                var index = Array.FindIndex(preferred, location => _fileSystem.ContainsSubPath(location, video.Path));
                return index < 0 ? int.MaxValue : index;
            }

            var defaultIds = videos.Where(v => v.PrimaryVersionId.HasValue).Select(v => v.PrimaryVersionId.Value).Distinct().ToList();
            foreach (var id in defaultIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_libraryManager.GetItemById(id) is not Video current)
                {
                    continue;
                }

                var group = _libraryManager.GetLinkedAlternateVersions(current).Prepend(current).ToList();
                var best = group.OrderBy(Rank).ThenByDescending(v => v.Width).First();
                if (Rank(best) == Rank(current) || !group.All(IsEligible))
                {
                    continue;
                }

                _logger.LogInformation("Making {Path} the default version", best.Path);
                await _videoVersions.SplitAsync(current.Id, user, cancellationToken).ConfigureAwait(false);
                best = _libraryManager.GetItemById(best.Id) as Video ?? best; // as the split left it
                best.LinkedAlternateVersions = group.Where(v => v.Id != best.Id)
                    .Select(v => new LinkedChild { ItemId = v.Id, Type = LinkedChildType.LinkedAlternateVersion })
                    .ToArray();
                await best.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
                await _videoVersions.MergeAsync(group.Select(v => v.Id).ToArray(), user, cancellationToken).ConfigureAwait(false);
                if (_libraryManager.GetItemById(best.Id) is Video { PrimaryVersionId: not null })
                {
                    _logger.LogWarning("Jellyfin kept another default version for {Path}", best.Path);
                }
            }
        }

        private List<Movie> GetMoviesFromLibrary()
        {
            return _libraryManager
                    .GetItemList(
                        new InternalItemsQuery
                        {
                            IncludeItemTypes = [BaseItemKind.Movie],
                            IsVirtualItem = false,
                            Recursive = true,
                        }
                )
                .Select(m => m as Movie)
                .Where(m => m.ProviderIds.ContainsKey("Tmdb"))
                .Where(IsEligible)
                .ToList();
        }

        private List<Episode> GetEpisodesFromLibrary()
        {
            return _libraryManager
                .GetItemList(
                    new InternalItemsQuery
                    {
                        IncludeItemTypes = [BaseItemKind.Episode],
                        IsVirtualItem = false,
                        Recursive = true,
                    }
                )
                .Select(m => m as Episode)
                .Where(IsEligible)
                .ToList();
        }

        private static string GetEpisodeMergeKey(Episode episode)
        {
            if (episode.ParentIndexNumber.HasValue && episode.IndexNumber.HasValue)
            {
                return $"number:{episode.SeriesName}:{episode.ParentIndexNumber}:{episode.IndexNumber}:{episode.IndexNumberEnd}";
            }

            foreach (var provider in new[] { "Tvdb", "Tmdb", "Imdb" })
            {
                if (episode.ProviderIds.TryGetValue(provider, out var providerId)
                    && !string.IsNullOrWhiteSpace(providerId))
                {
                    return $"provider:{episode.SeriesName}:{provider}:{providerId}";
                }
            }

            return $"title:{episode.SeriesName}:{episode.SeasonName}:{episode.Name}:{episode.ProductionYear}";
        }

        private bool IsEligible(BaseItem item)
        {
            if (IsInInactiveLibrary(item) || IsInExcludedLibrary(item))
            {
                return false;
            }
            return true;
        }

        private bool IsInExcludedLibrary(BaseItem item)
        {
           return Plugin.Instance.PluginConfiguration.LocationsExcluded != null
                  && Plugin.Instance.PluginConfiguration.LocationsExcluded
                    .Any(s => _fileSystem.ContainsSubPath(s, item.Path));
        }

        private bool IsInInactiveLibrary(BaseItem item)
        {
            if (item is not Movie)
            {
                return false;
            }

            var parentPath = item.DisplayParent?.Path;
            if (string.IsNullOrWhiteSpace(parentPath))
            {
                return false;
            }

            var virtualFolders = _libraryManager.GetVirtualFolders();

            return !virtualFolders
                .SelectMany(vf => vf.Locations ?? Array.Empty<string>())
                .Any(libPath => string.Equals(libPath, parentPath, StringComparison.OrdinalIgnoreCase) ||
                                _fileSystem.ContainsSubPath(libPath, parentPath));
        }
        private void OnTimerElapsed() { }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer?.Dispose();
            }
        }
    }
}
