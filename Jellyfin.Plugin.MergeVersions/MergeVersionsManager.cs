using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MergeVersions.Api;
using MediaBrowser.Model.IO;
using MediaBrowser.Api;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MergeVersions
{
    public class MergeVersionsManager : IServerEntryPoint
    {
        private readonly ILibraryManager _libraryManager;
        private readonly VideosService _videoService;
        private readonly Timer _timer;
        private readonly ILogger<VideosService> _logger; // TODO logging
        private readonly IUserManager _userManager;
        private readonly SessionInfo _session;
        private readonly ISessionManager _sessionManager;
        private readonly IFileSystem _fileSystem;


        public MergeVersionsManager(ILibraryManager libraryManager, ICollectionManager collectionManager, ILogger<VideosService> logger, IServerConfigurationManager serverConfigurationManager,
            IHttpResultFactory httpResultFactory,
            IUserManager userManager,
            IDtoService dtoService,
            IAuthorizationContext authContext,
            IFileSystem fileSystem)
        {
            
            
            _session = new SessionInfo(_sessionManager, logger);
            _libraryManager = libraryManager;
            _userManager = userManager;
            _logger = logger;
            _timer = new Timer(_ => OnTimerElapsed(), null, Timeout.Infinite, Timeout.Infinite);
            _videoService = new VideosService(_logger, serverConfigurationManager, httpResultFactory, libraryManager, userManager, dtoService, authContext);
            _fileSystem = fileSystem;       
        }



        private IEnumerable<Movie> GetMoviesFromLibrary()
        {
            var movies = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] {nameof(Movie)},
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
                IncludeItemTypes = new[] { nameof(Episode) },
                IsVirtualItem = false,
                Recursive = true,

            }).Select(m => m as Episode);

            return episodes.ToList();
        
    }

        public void MergeMovies(IProgress<double> progress)
        {
            var movies = GetMoviesFromLibrary().ToArray();
            //var baseItems = new List<BaseItem>();


            _logger.LogInformation("Scanning for repeated movies");

            //Group by the title and year, then select those with more than 1 in the group
            var duplications = movies.GroupBy(x => new {x.OriginalTitle, x.ProductionYear}).Where(x => x.Count() > 1).ToList();
            var total = duplications.Count();
            var current = 0;
            //foreach grouping, merge
            foreach (var m in duplications)
            {
                current++;
                var percent = ((double) current / (double) total) * 100;
                progress?.Report((int)percent);

                MergeVideos(m.Where(m => m.PrimaryVersionId == null && m.GetLinkedAlternateVersions().Count() == 0).ToList());//We only want non merged movies
                    
            }
            progress?.Report(100);
        }
        public void SplitMovies(IProgress<double> progress)
		{
            var movies = GetMoviesFromLibrary().ToArray();
            var total = movies.Count();
            var current = 0;
            //foreach grouping, merge
            foreach (var m in movies)
            {
                current++;
                var percent = ((double)current / (double)total) * 100;
                progress?.Report((int)percent);

                _logger.LogInformation($"Spliting {m.OriginalTitle} ({m.ProductionYear})");
                SplitVideo(m);
            }
            progress?.Report(100);

        }

		private void SplitVideo(BaseItem v)
		{
            var das = new MediaBrowser.Api.DeleteAlternateSources();
            das.Id = v.Id.ToString();
            _videoService.Delete(das);
            
        }

		private void MergeVideos(IEnumerable<BaseItem> videos)
        {
            List<BaseItem> elegibleToMerge = new List<BaseItem>();

            foreach (var video in videos)
			{
				if (isElegible(video))
				{
                    elegibleToMerge.Add(video);
				}
			}
            
            var mv = new MediaBrowser.Api.MergeVersions
            {
                Ids = String.Join(',', elegibleToMerge.Select(m => m.Id))
            };
            if (elegibleToMerge.Count() > 1)
            {
                _logger.LogInformation($"Merging {videos.ElementAt(0).OriginalTitle} ({videos.ElementAt(0).ProductionYear})");
                _logger.LogDebug($"ids are {mv.Ids}\nMerging...");
                _videoService.Post(mv);
                _logger.LogDebug("merged");
            }
        }

        public void MergeEpisodes(IProgress<double> progress)
        {
            var episodes = GetEpisodesFromLibrary().ToArray();

            _logger.LogInformation("Scanning for repeated episodes");

            //Group by the Series name, Season name , episode name, episode number and year, then select those with more than 1 in the group
            var duplications = episodes.GroupBy(x => new {x.SeriesName,x.SeasonName, x.OriginalTitle,x.IndexNumber, x.ProductionYear }).Where(x => x.Count() > 1).ToList();

           var total = duplications.Count();
            var current = 0;
            //foreach grouping, merge
            foreach (var e in duplications)
            {
                current++;
                var percent = ((double)current / (double)total) * 100;
                progress?.Report((int)percent);

                _logger.LogInformation($"Merging {e.Key.IndexNumber} ({e.Key.SeriesName})");
                MergeVideos(e.ToList().Where(e => e.PrimaryVersionId == null && e.GetLinkedAlternateVersions().Count()==0));//We only want non merged movies
            }
            progress?.Report(100);
        }

        public void SplitEpisodes(IProgress<double> progress)
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
                SplitVideo(e);
            }
            progress?.Report(100);

        }


        private bool isElegible(BaseItem item)
		{
            if (Plugin.Instance.Configuration.LocationsExcluded != null && Plugin.Instance.Configuration.LocationsExcluded.Any(s => _fileSystem.ContainsSubPath(s, item.Path)))
            {
                return false;
            }
            return true;
        }

        private void OnTimerElapsed()
        {
            // Stop the timer until next update
            _timer.Change(Timeout.Infinite, Timeout.Infinite);

        }

        public Task RunAsync()
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {

        }
    }
}
