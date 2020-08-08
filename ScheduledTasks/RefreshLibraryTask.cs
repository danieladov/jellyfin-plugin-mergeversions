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

namespace Jellyfin.Plugin.MergeVersions.ScheduledTasks
{
    public class RefreshLibraryTask : IScheduledTask
    {
        private readonly ILogger _logger;
        private readonly MergeVersionsManager _mergeVersionsManager;

        public RefreshLibraryTask(ILibraryManager libraryManager, ICollectionManager collectionManager, ILogger<VideosService> logger, IServerConfigurationManager serverConfigurationManager,
            IHttpResultFactory httpResultFactory,
            IUserManager userManager,
            IDtoService dtoService,
            IAuthorizationContext authContext)
        {
            _logger = logger;
            _mergeVersionsManager = new MergeVersionsManager(libraryManager, collectionManager, logger, serverConfigurationManager,
             httpResultFactory,
             userManager,
             dtoService,
             authContext, new GetId());
        }
        public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            _logger.LogInformation("Starting plugin refresh library task");
            _mergeVersionsManager.ScanLibrary(progress);
            _logger.LogInformation("plugin refresh library task finished");
            return Task.CompletedTask;
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            // Run this task every 24 hours
            return new[] {
                new TaskTriggerInfo { Type = TaskTriggerInfo.TriggerInterval, IntervalTicks = TimeSpan.FromHours(24).Ticks}
            };
        }

        public string Name => "Scan library to merge repeated movies";
        public string Key => "MergeVersionsRefreshLibraryTask";
        public string Description => "Scans all libraries to merge repeated movies";
        public string Category => "Merge Versions";
    }


    public class SplitLibraryTask : IScheduledTask
    {
        private readonly ILogger _logger;
        private readonly MergeVersionsManager _mergeVersionsManager;

        public SplitLibraryTask(ILibraryManager libraryManager, ICollectionManager collectionManager, ILogger<VideosService> logger, IServerConfigurationManager serverConfigurationManager,
            IHttpResultFactory httpResultFactory,
            IUserManager userManager,
            IDtoService dtoService,
            IAuthorizationContext authContext)
        {
            _logger = logger;
            _mergeVersionsManager = new MergeVersionsManager(libraryManager, collectionManager, logger, serverConfigurationManager,
             httpResultFactory,
             userManager,
             dtoService,
             authContext, new GetId());
        }
        public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            _logger.LogInformation("Starting plugin refresh library task");
            _mergeVersionsManager.SplitLibrary(progress);
            _logger.LogInformation("plugin refresh library task finished");
            return Task.CompletedTask;
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            // Run this task every 24 hours
            return new[] {
                new TaskTriggerInfo { Type = TaskTriggerInfo.TriggerInterval, IntervalTicks = TimeSpan.FromHours(24).Ticks}
            };
        }

        public string Name => "Split all merged movies";
        public string Key => "SplitLibraryTask";
        public string Description => "Splits all merged movies";
        public string Category => "Merge Versions";
    }
}
