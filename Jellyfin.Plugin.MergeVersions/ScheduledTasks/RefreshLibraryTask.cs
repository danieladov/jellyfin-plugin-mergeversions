using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MergeVersions.Api;
using MediaBrowser.Api;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using MediaBrowser.Model.IO;
using Jellyfin.Api.Controllers;
using MediaBrowser.Controller.Dlna;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Devices;
using Jellyfin.Api.Helpers;

namespace Jellyfin.Plugin.MergeVersions.ScheduledTasks
{
    public class MergeMoviesTask : IScheduledTask
    {
        private readonly ILogger _logger;
        private readonly MergeVersionsManager _mergeVersionsManager;

        public MergeMoviesTask(ILibraryManager libraryManager,
            ICollectionManager collectionManager,
            ILogger<VideosController> logger,
            IServerConfigurationManager serverConfigurationManager,
            IHttpResultFactory httpResultFactory,
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
            _logger = logger;
            _mergeVersionsManager = new MergeVersionsManager(libraryManager, collectionManager, logger, serverConfigurationManager,
             httpResultFactory,
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
        }
        public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            _logger.LogInformation("Starting plugin, Merging Movies");
            _mergeVersionsManager.MergeMovies(progress);
            _logger.LogInformation("All movies merged");
            return Task.CompletedTask;
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            // Run this task every 24 hours
            return new[] {
                new TaskTriggerInfo { Type = TaskTriggerInfo.TriggerInterval, IntervalTicks = TimeSpan.FromHours(24).Ticks}
            };
        }

        public string Name => "Merge All Movies";
        public string Key => "MergeMoviesTask";
        public string Description => "Scans all libraries to merge repeated movies";
        public string Category => "Merge Versions";
    }


    public class MergeEpisodesTask : IScheduledTask
    {
        private readonly ILogger _logger;
        private readonly MergeVersionsManager _mergeVersionsManager;

        public MergeEpisodesTask(ILibraryManager libraryManager,
            ICollectionManager collectionManager,
            ILogger<VideosController> logger,
            IServerConfigurationManager serverConfigurationManager,
            IHttpResultFactory httpResultFactory,
            IUserManager userManager,
            IDtoService dtoService,
            IAuthorizationContext authContext,
            IFileSystem fileSystem,
            IDlnaManager dlnaManager,
            IMediaSourceManager mediaSourceManager,
            IMediaEncoder mediaEncoder,
            ISubtitleEncoder subtitleEncoder,
            IDeviceManager deviceManager,
            TranscodingJobHelper transcodingJobHelper)
        {
            _logger = logger;
            _mergeVersionsManager = new MergeVersionsManager(libraryManager, collectionManager, logger, serverConfigurationManager,
             httpResultFactory,
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
        }
        public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            _logger.LogInformation("Starting plugin, Merging Episodes");
            _mergeVersionsManager.MergeEpisodes(progress);
            _logger.LogInformation("Merging Episodes task finished");
            return Task.CompletedTask;
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            // Run this task every 24 hours
            return new[] {
                new TaskTriggerInfo { Type = TaskTriggerInfo.TriggerInterval, IntervalTicks = TimeSpan.FromHours(24).Ticks}
            };
        }

        public string Name => "Merge All Episodes";
        public string Key => "MergeEpisodesTask";
        public string Description => "Merges all repeated episodes";
        public string Category => "Merge Versions";
    }
}
