using System;
using MediaBrowser.Api;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;
using Jellyfin.Api.Controllers;
using MediaBrowser.Controller.Dlna;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Devices;
using Jellyfin.Api.Helpers;

using System.Net.Mime;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MergeVersions.Api
{ 
     /// <summary>
    /// The Merge Versions api controller.
    /// </summary>
    [ApiController]
    [Authorize(Policy = "DefaultAuthorization")]
    [Route("MergeVersions")]
    [Produces(MediaTypeNames.Application.Json)]
    public class MergeVersionsController : ControllerBase
    {
        private readonly MergeVersionsManager _mergeVersionsManager;
        private readonly ILogger<VideosController> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="TMDbBoxSetsController"/>.

        public MergeVersionsController (ILibraryManager libraryManager,
            ICollectionManager collectionManager,
            ILogger<VideosController> logger,
            IServerConfigurationManager serverConfigurationManager,
            IUserManager userManager,
            IDtoService dtoService,
            IAuthorizationContext authContext,
            IFileSystem fileSystem,
            IDlnaManager dlnaManager,
            IMediaSourceManager mediaSourceManager,
            IMediaEncoder mediaEncoder,
            ISubtitleEncoder subtitleEncoder,
            IDeviceManager deviceManager,
            TranscodingJobHelper transcodingJobHelper

            )
        {
            _mergeVersionsManager = new MergeVersionsManager(libraryManager, collectionManager, logger, serverConfigurationManager,
             userManager,
             dtoService,
             authContext,
             fileSystem,
             dlnaManager,
             mediaSourceManager,
             mediaEncoder,
             subtitleEncoder,
             deviceManager,
             transcodingJobHelper);
            _logger = logger;
        }


        /// <summary>
        /// Scans all movies and merges repeated ones.
        /// </summary>
        /// <reponse code="204">Library scan and merge started successfully. </response>
        /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
        [HttpPost("MergeMovies")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public ActionResult MergeMoviesRequest()
        {
            _logger.LogInformation("Starting a manual refresh, looking up for repeated versions");
            _mergeVersionsManager.MergeMovies(null);
            _logger.LogInformation("Completed refresh");
            return NoContent();
        }

        /// <summary>
        /// Scans all movies and splits merged ones.
        /// </summary>
        /// <reponse code="204">Library scan and split started successfully. </response>
        /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
        [HttpPost("SplitMovies")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public ActionResult SplitMoviesRequest()
        {
            _logger.LogInformation("Spliting all movies");
            _mergeVersionsManager.SplitMovies(null);
            _logger.LogInformation("Completed");
            return NoContent();
        }


        /// <summary>
        /// Scans all episodes and merge repeated ones.
        /// </summary>
        /// <reponse code="204">Library scan and merge started successfully. </response>
        /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
        [HttpPost("MergeEpisodes")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public ActionResult MergeEpisodesRequest()
        {
            _logger.LogInformation("Starting a manual refresh, looking up for repeated versions");
            _mergeVersionsManager.MergeEpisodes(null);
            _logger.LogInformation("Completed refresh");
            return NoContent();
        }


        /// <summary>
        /// Scans all episodes and splits merged ones.
        /// </summary>
        /// <reponse code="204">Library scan and split started successfully. </response>
        /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
        [HttpPost("SplitEpisodes")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public ActionResult SplitEpisodesRequest()
        {
            _logger.LogInformation("Spliting all movies");
            _mergeVersionsManager.SplitEpisodes(null);
            _logger.LogInformation("Completed");
            return NoContent();
        }
    }
}