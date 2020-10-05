using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Entities;
using System;

namespace Jellyfin.Plugin.MergeVersions.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {

        public String[] selectedVirtualFolders { get; set; }

        public PluginConfiguration()
        {
            selectedVirtualFolders = Array.Empty<String>();
        }
    }
}
