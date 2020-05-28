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
        private readonly MergeVersionsManager _tmDbBoxSetManager;
        private readonly ILogger<VideosService> _logger;

        public MergeVersionsService(ILibraryManager libraryManager, ICollectionManager collectionManager, ILogger<VideosService> logger, IServerConfigurationManager serverConfigurationManager,
            IHttpResultFactory httpResultFactory,
            IUserManager userManager,
            IDtoService dtoService,
            IAuthorizationContext authContext)
        {
            _tmDbBoxSetManager = new MergeVersionsManager( libraryManager,  collectionManager,  logger,  serverConfigurationManager,
             httpResultFactory,
             userManager,
             dtoService,
             authContext,new GetId());
            _logger = logger;
        }
        
        public void Post(RefreshMetadataRequest request)
        {
            _logger.LogInformation("Starting a manual refresh, looking up for repeated versions");
            _tmDbBoxSetManager.ScanLibrary(null);
            _logger.LogInformation("Completed refresh");
        }
    }
}