using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using MediaBrowser.Model.IO;


namespace Jellyfin.Plugin.MergeVersions.ScheduledTasks
{
    public class MergeMoviesTask : IScheduledTask
    {
        private readonly ILogger _logger;
        private readonly MergeVersionsManager _mergeVersionsManager;

        public MergeMoviesTask(ILibraryManager libraryManager, ILogger<MergeVersionsManager> logger,
            IFileSystem fileSystem)
        {
            _logger = logger;
            _mergeVersionsManager = new MergeVersionsManager(libraryManager,logger,fileSystem);
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

        public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            return Execute(cancellationToken, progress);
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

        public MergeEpisodesTask(ILibraryManager libraryManager, ILogger<MergeVersionsManager> logger,
            IFileSystem fileSystem)
        {
            _logger = logger;
            _mergeVersionsManager = new MergeVersionsManager(
                libraryManager, logger,fileSystem);
        }
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            _logger.LogInformation("Starting plugin, Merging Episodes");
            await _mergeVersionsManager.MergeEpisodesAsync(progress);
            _logger.LogInformation("Merging Episodes task finished");
            return;
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            // Run this task every 24 hours
            return new[] {
                new TaskTriggerInfo { Type = TaskTriggerInfo.TriggerInterval, IntervalTicks = TimeSpan.FromHours(24).Ticks}
            };
        }

        public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            return Execute(cancellationToken, progress);
        }

        public string Name => "Merge All Episodes";
        public string Key => "MergeEpisodesTask";
        public string Description => "Merges all repeated episodes";
        public string Category => "Merge Versions";
    }
}
