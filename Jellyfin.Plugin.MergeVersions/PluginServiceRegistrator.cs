using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.MergeVersions
{
    public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
    {
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            serviceCollection.AddSingleton<IVideoVersions, JellyfinVideoVersions>();
            serviceCollection.AddSingleton<MergeVersionsManager>();
        }
    }
}
