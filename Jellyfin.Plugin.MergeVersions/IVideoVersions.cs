using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.MergeVersions
{
    public interface IVideoVersions
    {
        Task MergeAsync(Guid[] ids, ClaimsPrincipal user, CancellationToken cancellationToken);

        Task SplitAsync(Guid itemId, ClaimsPrincipal user, CancellationToken cancellationToken);
    }
}
