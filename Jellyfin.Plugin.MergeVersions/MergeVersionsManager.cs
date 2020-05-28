using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Session;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Dto;
using Microsoft.Extensions.Logging;
using System.Globalization;
using Jellyfin.Plugin.TMDbBoxSets.Api;
using MediaBrowser.Api;
using MediaBrowser.Model.Library;


namespace Jellyfin.Plugin.TMDbBoxSets
{
    public class MergeVersionsManager : IServerEntryPoint
    {
        private readonly ILibraryManager _libraryManager;
        private readonly VideosService _videoService;

    
        
        private readonly ICollectionManager _collectionManager;
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
            _session = new SessionInfo(_sessionManager,logger);
            _libraryManager = libraryManager;
            _userManager = userManager;
            _collectionManager = collectionManager;
            _logger = logger;
            _timer = new Timer(_ => OnTimerElapsed(), null, Timeout.Infinite, Timeout.Infinite);
            _videoService = new VideosService(_logger,serverConfigurationManager,httpResultFactory,libraryManager,userManager,dtoService,authContext);
        }

   

        private IReadOnlyCollection<Movie> GetMoviesFromLibrary()
        {
            var movies = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] {typeof(Movie).Name},
                IsVirtualItem = false,
                OrderBy = new List<ValueTuple<string, SortOrder>>
                {
                    new ValueTuple<string, SortOrder>(ItemSortBy.SortName, SortOrder.Ascending)
                },
                Recursive = true,
                HasTmdbId = true
                
            }).Select(m => m as Movie);

            return movies.ToList();
        }


 

       
        public void ScanLibrary(IProgress<double> progress)
        {
            InternalItemsQuery query=new InternalItemsQuery();


            var movies =GetMoviesFromLibrary().ToArray();
            var baseItems= new List<BaseItem>();


            _logger.LogInformation("Scanning for repeated movies");

            var user = new User();
  

            baseItems = _libraryManager.Sort(movies,user,new[] { ItemSortBy.SortName }, SortOrder.Descending).ToList();


            for (var i = 0;i <baseItems.Count(); i++)
            {
                List<BaseItem> sameMovies = new List<BaseItem>();
                for (var j = i+1; j < baseItems.Count(); j++)
                {
                    _logger.LogDebug("Comparing" + baseItems.ElementAt(i).OriginalTitle + "with" + baseItems.ElementAt(j).OriginalTitle);
                    

                    if (baseItems.ElementAt(i).OriginalTitle.Equals(baseItems.ElementAt(j).OriginalTitle))
                    {
                        if (sameMovies.Count() == 0)
                        {
                            sameMovies.Add(baseItems.ElementAt(i));
                        }
                        sameMovies.Add(baseItems.ElementAt(j));
                        i++;
                    }
                    else
                    {
                        break;
                    }
                 
                }
                if (sameMovies.Count > 0)
                {
                    mergeByList(sameMovies);
                   
                   
                }
                

            }



            progress?.Report(100);
        }

        private List<BaseItem> moviesToBaseItem(IReadOnlyCollection<Movie> movies)
        {
            List<BaseItem> baseItems = new List<BaseItem>();
            foreach(Movie movie in movies){
                baseItems.Append((BaseItem)movie);
            }
            return baseItems;
        }


        private void mergeByList(IReadOnlyCollection<BaseItem> movies)
        {
            _logger.LogDebug("Creating ids to merge ");
            MergeVersions mv = new MergeVersions();
            var id ="";
            for(int i = 0;i<movies.Count();i++)
            {
                id += movies.ElementAt(i).Id;
                if (i < movies.Count() - 1)
                {
                    id += ",";
                }
                
            }
            id.Remove(id.Length - 1,1);
            _logger.LogDebug("ids is " + id);
            mv.Ids = id;
            _logger.LogDebug("merging...");

            _videoService.Post(mv);
            _logger.LogDebug("merged");


        }
        

        private void OnTimerElapsed()
        {
            // Stop the timer until next update
            _timer.Change(Timeout.Infinite, Timeout.Infinite);

        }

        public void Dispose()
        {
            
        }

        public Task RunAsync()
        {

            return Task.CompletedTask;
        }
    }
}
