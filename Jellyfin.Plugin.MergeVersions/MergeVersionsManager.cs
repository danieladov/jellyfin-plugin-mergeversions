using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MergeVersions
{
    public class MergeVersionsManager : IDisposable
    {
        private readonly ILibraryManager _libraryManager;
        private readonly Timer _timer;
        private readonly ILogger<MergeVersionsManager> _logger; // TODO logging
        private readonly IFileSystem _fileSystem;

        public MergeVersionsManager(
            ILibraryManager libraryManager,
            ILogger<MergeVersionsManager> logger,
            IFileSystem fileSystem
        )
        {
            _libraryManager = libraryManager;
            _logger = logger;
            _fileSystem = fileSystem;
            _timer = new Timer(_ => OnTimerElapsed(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public void MergeMovies(IProgress<double> progress)
        {
            _logger.LogInformation("Scanning for repeated movies");

            var duplicateMovies = GetMoviesFromLibrary()
                .GroupBy(x => x.ProviderIds["Tmdb"])
                .Where(group => group.Count() > 1 &&
                group.Any(movie => movie.PrimaryVersionId == null &&
                                    !movie.LinkedAlternateVersions.Any()))
                .ToList();

            var current = 0;
            Parallel.ForEach(
                duplicateMovies,
                async m =>
                {
                    current++;
                    var percent = current / (double)duplicateMovies.Count * 100;
                    progress?.Report((int)percent);
                    _logger.LogInformation(
                        $"Merging {m.ElementAt(0).Name} ({m.ElementAt(0).ProductionYear})"
                    );
                    await MergeVersions(m.Select(e => e.Id).ToList());
                }
            );
            progress?.Report(100);
        }

        public void SplitMovies(IProgress<double> progress)
        {
            var movies = GetMoviesFromLibrary();
            var current = 0;
            Parallel.ForEach(
                movies,
                async m =>
                {
                    current++;
                    var percent = current / (double)movies.Count * 100;
                    progress?.Report((int)percent);

                    _logger.LogInformation($"Spliting {m.Name} ({m.ProductionYear})");
                    await DeleteAlternateSources(m.Id);
                }
            );
            progress?.Report(100);
        }

        public async Task MergeEpisodesAsync(IProgress<double> progress)
        {
            _logger.LogInformation("Scanning for repeated episodes");

            var episodes = GetEpisodesFromLibrary();
            var duplicateEpisodes = episodes
                .GroupBy(GetEpisodeMergeKey, StringComparer.OrdinalIgnoreCase)
                .Where(x => x.Count() > 1)
                .ToList();

            _logger.LogInformation(
                "Found {EpisodeCount} episodes and {DuplicateGroupCount} duplicate episode groups",
                episodes.Count,
                duplicateEpisodes.Count);

            var current = 0;
            foreach (var e in duplicateEpisodes)
            {
                current++;
                var percent = current / (double)duplicateEpisodes.Count * 100;
                progress?.Report((int)percent);
                _logger.LogInformation(
                    $"Merging {e.ElementAt(0).Name} ({e.ElementAt(0).ProductionYear})"
                );
                await MergeVersions(e.Select(e => e.Id).ToList());
            }
            progress?.Report(100);
        }

        public async Task SplitEpisodesAsync(IProgress<double> progress)
        {
            var episodes = GetEpisodesFromLibrary();
            var current = 0;

            foreach (var e in episodes)
            {
                current++;
                var percent = current / (double)episodes.Count * 100;
                progress?.Report((int)percent);

                _logger.LogInformation($"Splitting {e.IndexNumber} ({e.SeriesName})");
                await DeleteAlternateSources(e.Id);
            }
            progress?.Report(100);
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
            foreach (var provider in new[] { "Tvdb", "Tmdb", "Imdb" })
            {
                if (episode.ProviderIds.TryGetValue(provider, out var providerId)
                    && !string.IsNullOrWhiteSpace(providerId))
                {
                    return $"provider:{provider}:{providerId}";
                }
            }
            
            if (episode.ParentIndexNumber.HasValue && episode.IndexNumber.HasValue)
            {
                return $"number:{episode.SeriesName}:{episode.ParentIndexNumber}:{episode.IndexNumber}:{episode.IndexNumberEnd}";
            }

            return $"title:{episode.SeriesName}:{episode.SeasonName}:{episode.Name}:{episode.ProductionYear}";
        }

        private async Task MergeVersions(List<Guid> ids)
        {
            var items = ids
                .Select(i => _libraryManager.GetItemById<BaseItem>(i, null))
                .OfType<Video>()
                .OrderBy(i => i.Id)
                .ToList();

            if (items.Count < 2)
            {
                return;
            }

            var primaryVersion = items.FirstOrDefault(i => i.MediaSourceCount > 1 && !i.PrimaryVersionId.HasValue);
            if (primaryVersion is null)
            {
                primaryVersion = items
                    .OrderBy(i => i.Video3DFormat.HasValue || i.VideoType != VideoType.VideoFile ? 1 : 0)
                    .ThenByDescending(i => i.GetDefaultVideoStream()?.Width ?? 0)
                    .First();
            }
            
            var versions = GetAllAlternateVersions(items);
            var alternateVersions = versions.Where(i => !i.Id.Equals(primaryVersion.Id)).ToList();


            foreach (var item in alternateVersions)
            {
                item.SetPrimaryVersionId(primaryVersion.Id);
                item.OwnerId = primaryVersion.Id;
                PreserveAlternateVersionLinks(item);

                await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None).ConfigureAwait(false);
                await _libraryManager.RerouteLinkedChildReferencesAsync(item.Id, primaryVersion.Id).ConfigureAwait(false);
            }

            foreach (var item in alternateVersions)
            {
                item.LocalAlternateVersions = Array.Empty<string>();
                item.LinkedAlternateVersions = Array.Empty<LinkedChild>();
                await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None).ConfigureAwait(false);
            }

            primaryVersion.LocalAlternateVersions = alternateVersions
                .Select(i => i.Path)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            primaryVersion.LinkedAlternateVersions = Array.Empty<LinkedChild>();
            primaryVersion.SetPrimaryVersionId(null);
            primaryVersion.OwnerId = Guid.Empty;

            await primaryVersion.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None).ConfigureAwait(false);
            
            foreach (var alternate in alternateVersions)
            {
                _libraryManager.UpsertLinkedChild(
                    primaryVersion.Id,
                    alternate.Id,
                    LinkedChildType.LocalAlternateVersion);
            }

            _logger.LogInformation(
                "Merged {Count} local alternate versions into {Name} ({Id})",
                alternateVersions.Count,
                primaryVersion.Name,
                primaryVersion.Id);
        }

        private async Task DeleteAlternateSources(Guid itemId)
        {
            var item = _libraryManager.GetItemById<Video>(itemId);
            if (item is null)
            {
                return;
            }

            if (item.PrimaryVersionId.HasValue)
            {
                item = _libraryManager.GetItemById<Video>(item.PrimaryVersionId.Value);
            }

            if (item is null)
            {
                return;
            }

            var alternateVersions = GetAllAlternateVersions([item])
                .Where(i => !i.Id.Equals(item.Id))
                .ToList();

            _logger.LogInformation(
                "Splitting {Count} alternate versions from {Name} ({Id})",
                alternateVersions.Count,
                item.Name,
                item.Id);
            
            foreach (var alternate in alternateVersions)
            {
                alternate.SetPrimaryVersionId(null);
                alternate.OwnerId = Guid.Empty;
                PreserveAlternateVersionLinks(alternate);

                await alternate.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None).ConfigureAwait(false);
            }

            foreach (var alternate in alternateVersions)
            {
                alternate.LocalAlternateVersions = Array.Empty<string>();
                alternate.LinkedAlternateVersions = Array.Empty<LinkedChild>();
                await alternate.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None).ConfigureAwait(false);
            }

            item.LocalAlternateVersions = Array.Empty<string>();
            item.LinkedAlternateVersions = Array.Empty<LinkedChild>();
            item.SetPrimaryVersionId(null);
            item.OwnerId = Guid.Empty;
            await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None).ConfigureAwait(false);
        }

        private List<Video> GetAllAlternateVersions(IEnumerable<Video> initialVersions)
        {
            var versions = new Dictionary<Guid, Video>();
            var pending = new Queue<Video>(initialVersions);

            while (pending.Count > 0)
            {
                var version = pending.Dequeue();
                if (!versions.TryAdd(version.Id, version))
                {
                    continue;
                }

                foreach (var alternateId in _libraryManager.GetLocalAlternateVersionIds(version))
                {
                    if (_libraryManager.GetItemById<Video>(alternateId) is Video alternate)
                    {
                        pending.Enqueue(alternate);
                    }
                }

                foreach (var alternate in _libraryManager.GetLinkedAlternateVersions(version))
                {
                    pending.Enqueue(alternate);
                }
            }

            return versions.Values.ToList();
        }

        private void PreserveAlternateVersionLinks(Video version)
        {
            version.LocalAlternateVersions = _libraryManager.GetLocalAlternateVersionIds(version)
                .Select(id => _libraryManager.GetItemById<Video>(id)?.Path)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Cast<string>()
                .ToArray();
            version.LinkedAlternateVersions = _libraryManager.GetLinkedAlternateVersions(version)
                .Select(alternate => new LinkedChild
                {
                    ItemId = alternate.Id,
                    Type = LinkedChildType.LinkedAlternateVersion
                })
                .ToArray();
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
