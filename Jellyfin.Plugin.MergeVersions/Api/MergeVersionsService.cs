using System;
using MediaBrowser.Api;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MergeVersions.Api
{
    [Route("/MergeVersions/Refresh", "POST", Summary = "Merges all repeated movies")]
    

    

    [Authenticated]
    public class RefreshMetadataRequest : IReturnVoid
    {
    }

    
   

    [Route("/MergeVersions/SplitMovies", "DELETE", Summary = "Splits all merged movies")]
    public class SplitMoviesRequest : IReturnVoid
    {
    }

    [Route("/MergeVersions/MergeEpisodes", "POST", Summary = "Scans all TV Series and merges repeated episodes")]

    public class MergeEpisodesRequest : IReturnVoid
    {
    }

    [Route("/MergeVersions/SplitEpisodes", "DELETE", Summary = "Splits all merged Episodes")]
    public class SplitEpisodesRequest : IReturnVoid
    {
    }

    public class MergeVersionsService : IService
    {
        private readonly MergeVersionsManager _mergeVersionsManager;
        private readonly ILogger<VideosService> _logger;
        private readonly IProgress<double> progress;

        public MergeVersionsService(ILibraryManager libraryManager, ICollectionManager collectionManager, ILogger<VideosService> logger, IServerConfigurationManager serverConfigurationManager,
            IHttpResultFactory httpResultFactory,
            IUserManager userManager,
            IDtoService dtoService,
            IAuthorizationContext authContext,
            IFileSystem fileSystem
            )
        {
            _mergeVersionsManager = new MergeVersionsManager( libraryManager,  collectionManager,  logger,  serverConfigurationManager,
             httpResultFactory,
             userManager,
             dtoService,
             authContext,
             fileSystem);
            _logger = logger;
        }
        
        public void Post(RefreshMetadataRequest request)
        {
            _logger.LogInformation("Starting a manual refresh, looking up for repeated versions");
            _mergeVersionsManager.MergeMovies(progress);
            _logger.LogInformation("Completed refresh");
        }

        public void Post(MergeEpisodesRequest request)
        {
            _logger.LogInformation("Starting a manual refresh, looking up for repeated versions");
            _mergeVersionsManager.MergeEpisodes(progress);
            _logger.LogInformation("Completed refresh");
        }

        public void Delete(SplitMoviesRequest request)
        {
            _logger.LogInformation("Spliting all movies");
            _mergeVersionsManager.SplitMovies(progress);
            _logger.LogInformation("Completed");
        }

        public void Delete(SplitEpisodesRequest request)
        {
            _logger.LogInformation("Spliting all movies");
            _mergeVersionsManager.SplitEpisodes(progress);
            _logger.LogInformation("Completed");
        }

    }
}