using Microsoft.AspNetCore.Mvc;

// Test double for the server's runtime-only controller contract. These tests exercise MVC discovery,
// DI lifetime and dispatch, not Jellyfin's database implementation. Never shipped in the plugin.
namespace Jellyfin.Api.Controllers;

public sealed class InvocationState
{
    public List<VideosController> Controllers { get; } = new();

    public Func<Task<ActionResult>> Run { get; set; } = () => Task.FromResult<ActionResult>(new NoContentResult());
}

[Route("Videos")]
public sealed class VideosController : ControllerBase, IDisposable
{
    private readonly InvocationState _state;

    public VideosController(InvocationState state)
    {
        _state = state;
        state.Controllers.Add(this);
    }

    public Guid[] MergedIds { get; private set; }

    public Guid? SplitId { get; private set; }

    public bool Disposed { get; private set; }

    [HttpPost("MergeVersions")]
    public Task<ActionResult> MergeVersions(Guid[] ids)
    {
        MergedIds = ids;
        return _state.Run();
    }

    [HttpDelete("{itemId}/AlternateSources")]
    public Task<ActionResult> DeleteAlternateSources(Guid itemId)
    {
        SplitId = itemId;
        return _state.Run();
    }

    [NonAction]
    public void Dispose() => Disposed = true;
}
