using System;
using System.Collections.Generic;
using Jellyfin.Plugin.MergeVersions.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.MergeVersions
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages 
    {
        public Plugin(IApplicationPaths appPaths, IXmlSerializer xmlSerializer)
            : base(appPaths, xmlSerializer)
        {
            Instance = this;
        }

        public override string Name => "Merge Versions";

        public static Plugin Instance { get; private set; }

        public override string Description
            => "Merge Versions";

        public PluginConfiguration PluginConfiguration => Configuration;

        private readonly Guid _id = new Guid("f21bbed8-3a97-4d8b-88b2-48aaa65427cb");
        public override Guid Id => _id;

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "Merge Versions",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.configurationpage.html"
                }
            };
        }
    }
}
