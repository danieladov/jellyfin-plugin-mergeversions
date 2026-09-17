<h1 align="center">Jellyfin Merge Versions Plugin</h1>
<h3 align="center">Part of the <a href="https://jellyfin.org">Jellyfin Project</a></h3>

<p align="center">
Jellyfin Merge Versions plugin is a plugin that automatically groups every repeated movie and episode.

</p>

## Install Process


## From Repository
1. In jellyfin, go to dashboard -> plugins -> Repositories -> add and paste this link https://raw.githubusercontent.com/danieladov/JellyfinPluginManifest/master/manifest.json
2. Go to Catalog and search for the plugin you want to install
3. Click on it and install
4. Restart Jellyfin


## From .zip file
1. Download the .zip file from release page
2. Extract it and place the .dll file in a folder called ```plugins/Merge Versions``` under  the program data directory or inside the portable install directory
3. Restart Jellyfin

## User Guide
1. To merge your movies or episodes you can do it from Schedule task or directly from the configuration of the plugin.
2. Spliting is only avaible through the configuration

Merge and split use the installed Jellyfin server's native video-version actions. No URL or API key configuration is needed. Manual operations require administrator permissions.

This does not migrate or repair local-version groups created by earlier plugin releases. Split follows Jellyfin's native behavior for linked alternate versions; it does not detach local alternate versions.

Cancellation stops the scan between groups, after the current native operation finishes.



## Build Process
1. Clone or download this repository
2. Ensure you have .NET Core SDK setup and installed
3. Build plugin with following command.
```sh
dotnet publish Jellyfin.Plugin.MergeVersions/Jellyfin.Plugin.MergeVersions.csproj --configuration Release --output bin
```
4. Place the resulting .dll file in a folder called ```plugins/Merge versions``` under  the program data directory or inside the portable install directory

## Tests

```sh
dotnet test Jellyfin.Plugin.MergeVersions.sln --configuration Release
```

The tests cover controller discovery/dispatch using a test double, DI scope lifetime, error handling, authorization requirements, asynchronous completion and cancellation. They do not replace a merge/split smoke test against a Jellyfin test library.

The adapter discovers `Jellyfin.Api.Controllers.VideosController` through MVC and resolves it from Jellyfin's service container. If Jellyfin changes the action signatures, operations fail with a compatibility error instead of falling back to manual relationship changes.


