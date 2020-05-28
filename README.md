<h1 align="center">Jellyfin Merge Versions Plugin</h1>
<h3 align="center">Part of the <a href="https://jellyfin.media">Jellyfin Project</a></h3>

<p align="center">
Jellyfin Merge Versions plugin is a plugin that automatically groups every repeated movie;

</p>

## Build Process
1. Clone or download this repository
2. Ensure you have .NET Core SDK setup and installed
3. Build plugin with following command.
```sh
dotnet publish --configuration Release --output bin
```
4. Place the resulting .dll file in a folder called ```plugins/``` under  the program data directory or inside the portable install directory
