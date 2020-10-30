using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Entities;
using System;

namespace Jellyfin.Plugin.MergeVersions.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {

        public String[] LocationsExcluded { get; set; }

        public PluginConfiguration()
        {
            LocationsExcluded = Array.Empty<String>();
        }
    }
}
