using System;
using MediaBrowser.Api;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MergeVersions.Api
{
    [Route("/TMDbBoxSets/Refresh", "POST", Summary = "Scans all movies and creates box sets")]
    

    [Route("/TMDbBoxSets/split", "DELETE", Summary = "Splits all merged movies")]

    [Authenticated]
    public class RefreshMetadataRequest : IReturnVoid
    {
    }
    public class GetId 
    {
        [ApiMember(Name = "Id", Description = "Item Id", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "GET")]
        public Guid Id { get; set; }
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
            IAuthorizationContext authContext)
        {
            _mergeVersionsManager = new MergeVersionsManager( libraryManager,  collectionManager,  logger,  serverConfigurationManager,
             httpResultFactory,
             userManager,
             dtoService,
             authContext,new GetId());
            _logger = logger;
        }
        
        public void Post(RefreshMetadataRequest request)
        {
            _logger.LogInformation("Starting a manual refresh, looking up for repeated versions");
            _mergeVersionsManager.ScanLibrary(progress);
            _logger.LogInformation("Completed refresh");
        }

        public void Delete(RefreshMetadataRequest request)
        {
            _logger.LogInformation("Spliting all movies");
            _mergeVersionsManager.SplitLibrary(progress);
            _logger.LogInformation("Completed");
        }

    }
}