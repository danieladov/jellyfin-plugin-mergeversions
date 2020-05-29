using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MergeVersions.Api;
using MediaBrowser.Api;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MergeVersions
{
    public class MergeVersionsManager : IServerEntryPoint
    {
        private readonly ILibraryManager _libraryManager;
        private readonly VideosService _videoService;
        private readonly Timer _timer;
        private readonly ILogger<VideosService> _logger; // TODO logging
        private readonly UserService _userservice;
        private readonly IUserManager _userManager;
        private readonly SessionInfo _session;
        private readonly ISessionManager _sessionManager;


        public MergeVersionsManager(ILibraryManager libraryManager, ICollectionManager collectionManager, ILogger<VideosService> logger, IServerConfigurationManager serverConfigurationManager,
            IHttpResultFactory httpResultFactory,
            IUserManager userManager,
            IDtoService dtoService,
            IAuthorizationContext authContext, GetId request)
        {
            var id = request.Id;
            _session = new SessionInfo(_sessionManager, logger);
            _libraryManager = libraryManager;
            _userManager = userManager;
            _logger = logger;
            _timer = new Timer(_ => OnTimerElapsed(), null, Timeout.Infinite, Timeout.Infinite);
            _videoService = new VideosService(_logger, serverConfigurationManager, httpResultFactory, libraryManager, userManager, dtoService, authContext);
        }



        private IEnumerable<Movie> GetMoviesFromLibrary()
        {
            var movies = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] {nameof(Movie)},
                IsVirtualItem = false,
                Recursive = true,
                HasTmdbId = true

            }).Select(m => m as Movie);

            return movies.ToList();
        }

        public void ScanLibrary(IProgress<double> progress)
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

                _logger.LogInformation($"Merging {m.Key.OriginalTitle} ({m.Key.ProductionYear})");
                MergeMovies(m.ToList().Where(m => m.PrimaryVersionId == null));//We only want non merged movies
            }
            progress?.Report(100);
        }

        private void MergeMovies(IEnumerable<BaseItem> movies)
        {
            var mv = new MediaBrowser.Api.MergeVersions
            {
                Ids = String.Join(',', movies.Select(m => m.Id))
            };
            if (movies.Count() > 1)
            {
                _logger.LogDebug($"ids are {mv.Ids}\nMerging...");
                _videoService.Post(mv);
                _logger.LogDebug("merged");
            }
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
