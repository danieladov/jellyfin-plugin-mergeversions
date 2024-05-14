using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.IO;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Logging;
using Jellyfin.Data.Enums;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Globalization;



namespace Jellyfin.Plugin.MergeVersions
{
    public class MergeVersionsManager : IDisposable
    {
        private readonly ILibraryManager _libraryManager;
        private readonly Timer _timer;
        private readonly ILogger<MergeVersionsManager> _logger; // TODO logging
        private readonly SessionInfo _session;
        private readonly IFileSystem _fileSystem;


        public MergeVersionsManager(ILibraryManager libraryManager, ILogger<MergeVersionsManager> logger,
            IFileSystem fileSystem)
        {
            
            _libraryManager = libraryManager;
            _logger = logger;
            _fileSystem = fileSystem;
            _timer = new Timer(_ => OnTimerElapsed(), null, Timeout.Infinite, Timeout.Infinite);

        }



        private IEnumerable<Movie> GetMoviesFromLibrary()
        {
            var movies = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie },
                IsVirtualItem = false,
                Recursive = true,
                HasTmdbId = true,
                
                

            }).Select(m => m as Movie);

            return movies.ToList();
        }

        private IEnumerable<Episode> GetEpisodesFromLibrary()
		{
            var episodes = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Episode},
                IsVirtualItem = false,
                Recursive = true,

            }).Select(m => m as Episode);

            return episodes.ToList();
        
    }

        public void MergeMovies(IProgress<double> progress)
        {
            var movies = GetMoviesFromLibrary().ToArray();
            


            _logger.LogInformation("Scanning for repeated movies");

            //Group by Tmdb Id, then select those with more than 1 in the group
            var duplications = movies.GroupBy(x => new { V = x.ProviderIds["Tmdb"]}).Where(x => x.Count() > 1).ToList();
            var total = duplications.Count();
            var current = 0;
            //foreach grouping, merge
            Parallel.ForEach(duplications, async m => 
            {
                current++;
                var percent = ((double) current / (double) total) * 100;
                progress?.Report((int)percent);

                await MergeVideosAsync(m.Where(m => m.PrimaryVersionId == null && m.GetLinkedAlternateVersions().Count() == 0).ToList());//We only want non merged movies
                    
            }
                );
            //foreach (var m in duplications)
            progress?.Report(100);
        }
        public void SplitMovies(IProgress<double> progress)
		{
            var movies = GetMoviesFromLibrary().ToArray();
            movies = movies.Where(isElegible).ToArray();
            var total = movies.Count();
            var current = 0;
            //foreach grouping, merge
            Parallel.ForEach(movies, async m =>
             {
                 current++;
                 var percent = ((double)current / (double)total) * 100;
                 progress?.Report((int)percent);

                 _logger.LogInformation($"Spliting {m.Name} ({m.ProductionYear})");
                 await SplitVideoAsync(m);
             }
            );
            progress?.Report(100);

        }

		private async Task SplitVideoAsync(BaseItem v)
		{
            await DeleteAlternateSources(v.Id);
            
        }

		private async Task MergeVideosAsync(IEnumerable<BaseItem> videos)
        {
            List<BaseItem> elegibleToMerge = new List<BaseItem>();

            foreach (var video in videos)
			{
				if (isElegible(video))
				{
                    elegibleToMerge.Add(video);
				}
			}

            Guid[] ids = new Guid[elegibleToMerge.Count];
            for (int i = 0; i < elegibleToMerge.Count; i++)
			{
                ids[i] = elegibleToMerge.ElementAt(i).Id;
			}
                if (elegibleToMerge.Count() > 1)
            {
                _logger.LogInformation($"Merging {videos.ElementAt(0).Name} ({videos.ElementAt(0).ProductionYear})");
                _logger.LogDebug($"ids are " + printIds(ids)  + " Merging...");
                try
                {
				await MergeVersions(ids);

                }catch( Exception e)
                {
                    _logger.LogError("Error merging " + e.Message);
                }
                _logger.LogDebug("merged");
            }
        }

        private String printIds(Guid[] ids)
		{
            String aux = "";
            foreach(Guid id in ids)
			{
                aux += id;
                aux += " , ";
			}
            return aux;

		}

        public async Task MergeEpisodesAsync(IProgress<double> progress)
        {
            var episodes = GetEpisodesFromLibrary().ToArray();

            _logger.LogInformation("Scanning for repeated episodes");

            //Group by the Series name, Season name , episode name, episode number and year, then select those with more than 1 in the group
            var duplications = episodes.GroupBy(x => new {x.SeriesName,x.SeasonName, x.Name,x.IndexNumber, x.ProductionYear }).Where(x => x.Count() > 1).ToList();

           var total = duplications.Count();
            var current = 0;
            //foreach grouping, merge
            foreach (var e in duplications)
            {
                current++;
                var percent = ((double)current / (double)total) * 100;
                progress?.Report((int)percent);

                _logger.LogInformation($"Merging {e.Key.IndexNumber} ({e.Key.SeriesName})");
                await MergeVideosAsync(e.ToList().Where(e => e.PrimaryVersionId == null && e.GetLinkedAlternateVersions().Count()==0));//We only want non merged movies
            }
            progress?.Report(100);
        }

        public async Task SplitEpisodesAsync(IProgress<double> progress)
        {
            var episodes = GetEpisodesFromLibrary().ToArray();
            var total = episodes.Count();
            var current = 0;
            //foreach grouping, merge
            foreach (var e in episodes)
            {
                current++;
                var percent = ((double)current / (double)total) * 100;
                progress?.Report((int)percent);

                _logger.LogInformation($"Spliting {e.IndexNumber} ({e.SeriesName})");
                await SplitVideoAsync(e);
            }
            progress?.Report(100);

        }

        public async Task MergeVersions(Guid[] ids)
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

            var primaryVersion = items.FirstOrDefault(i => i.MediaSourceCount > 1 && string.IsNullOrEmpty(i.PrimaryVersionId));
            if (primaryVersion is null)
            {
                primaryVersion = items
                    .OrderBy(i =>
                    {
                        if (i.Video3DFormat.HasValue || i.VideoType != VideoType.VideoFile)
                        {
                            return 1;
                        }

                        return 0;
                    })
                    .ThenByDescending(i => i.GetDefaultVideoStream()?.Width ?? 0)
                    .First();
            }

            var alternateVersionsOfPrimary = primaryVersion.LinkedAlternateVersions.ToList();

            foreach (var item in items.Where(i => !i.Id.Equals(primaryVersion.Id)))
            {
                item.SetPrimaryVersionId(primaryVersion.Id.ToString("N", CultureInfo.InvariantCulture));

                await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None).ConfigureAwait(false);

                if (!alternateVersionsOfPrimary.Any(i => string.Equals(i.Path, item.Path, StringComparison.OrdinalIgnoreCase)))
                {
                    alternateVersionsOfPrimary.Add(new LinkedChild
                    {
                        Path = item.Path,
                        ItemId = item.Id
                    });
                }

                foreach (var linkedItem in item.LinkedAlternateVersions)
                {
                    if (!alternateVersionsOfPrimary.Any(i => string.Equals(i.Path, linkedItem.Path, StringComparison.OrdinalIgnoreCase)))
                    {
                        alternateVersionsOfPrimary.Add(linkedItem);
                    }
                }

                if (item.LinkedAlternateVersions.Length > 0)
                {
                    item.LinkedAlternateVersions = Array.Empty<LinkedChild>();
                    await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None).ConfigureAwait(false);
                }
            }

            primaryVersion.LinkedAlternateVersions = alternateVersionsOfPrimary.ToArray();
            await primaryVersion.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None).ConfigureAwait(false);
            return;
        }

        public async Task<ActionResult> DeleteAlternateSources([FromRoute, Required] Guid itemId)
        {
            var item = _libraryManager.GetItemById<Video>(itemId);
            if (item is null)
            {
                return null;
            }

            if (item.LinkedAlternateVersions.Length == 0 && item.PrimaryVersionId != null)
            {
                item = _libraryManager.GetItemById<Video>(Guid.Parse(item.PrimaryVersionId));
            }

            if (item is null)
            {
                return null;
            }

            foreach (var link in item.GetLinkedAlternateVersions())
            {
                link.SetPrimaryVersionId(null);
                link.LinkedAlternateVersions = Array.Empty<LinkedChild>();

                await link.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None).ConfigureAwait(false);
            }

            item.LinkedAlternateVersions = Array.Empty<LinkedChild>();
            item.SetPrimaryVersionId(null);
            await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None).ConfigureAwait(false);

            return null;
        }


        private bool isElegible(BaseItem item)
		{
            if (Plugin.Instance.PluginConfiguration.LocationsExcluded != null && Plugin.Instance.PluginConfiguration.LocationsExcluded.Any(s => _fileSystem.ContainsSubPath(s, item.Path)))
            {
                return false;
            }
            return true;
        }

        private void OnTimerElapsed()
        {
        }

        public Task RunAsync()
        {
            return Task.CompletedTask;
        }

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
                _session?.DisposeAsync();
            }
        }
    }
}
