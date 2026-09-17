using System;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.MergeVersions
{
    /// <summary>
    /// Calls the installed server's version actions without an HTTP round trip or a bundled Jellyfin.Api assembly.
    /// </summary>
    public sealed class JellyfinVideoVersions : IVideoVersions
    {
        private const string ControllerName = "Jellyfin.Api.Controllers.VideosController";
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Lazy<(Type Controller, MethodInfo Merge, MethodInfo Split)> _actions;

        public JellyfinVideoVersions(
            IServiceScopeFactory scopeFactory,
            IActionDescriptorCollectionProvider actionDescriptors)
        {
            _scopeFactory = scopeFactory;
            // MVC already knows the controller type from the server's assembly. Resolve it lazily,
            // after both Jellyfin and the plugin have finished registering their controllers.
            _actions = new Lazy<(Type, MethodInfo, MethodInfo)>(() =>
            {
                var type = actionDescriptors.ActionDescriptors.Items
                    .OfType<ControllerActionDescriptor>()
                    .Select(action => action.ControllerTypeInfo.AsType())
                    .Distinct()
                    .SingleOrDefault(candidate => candidate.FullName == ControllerName);

                if (type == null || !typeof(ControllerBase).IsAssignableFrom(type))
                {
                    throw new NotSupportedException("The installed Jellyfin server does not expose VideosController.");
                }

                return (type, GetAction(type, "MergeVersions", typeof(Guid[])),
                    GetAction(type, "DeleteAlternateSources", typeof(Guid)));
            });
        }

        public Task MergeAsync(Guid[] ids, ClaimsPrincipal user, CancellationToken cancellationToken)
            => InvokeAsync(ids, true, user, cancellationToken);

        public Task SplitAsync(Guid itemId, ClaimsPrincipal user, CancellationToken cancellationToken)
            => InvokeAsync(itemId, false, user, cancellationToken);

        private async Task InvokeAsync<T>(T argument, bool merge, ClaimsPrincipal user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var actions = _actions.Value;
            var method = merge ? actions.Merge : actions.Split;

            await using var scope = _scopeFactory.CreateAsyncScope();
            var controller = (ControllerBase)scope.ServiceProvider.GetRequiredService(actions.Controller);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = scope.ServiceProvider,
                    RequestAborted = cancellationToken,
                    // Scheduled jobs intentionally run without a user (Jellyfin's Guid.Empty/system context).
                    // Direct calls do not run MVC authorization filters: manual entry points MUST require elevation.
                    User = user ?? new ClaimsPrincipal(new ClaimsIdentity())
                }
            };

            // A bound delegate preserves the original exception instead of wrapping it in TargetInvocationException.
            var action = method.CreateDelegate<Func<T, Task<ActionResult>>>(controller);
            // Jellyfin's actions currently do not accept cancellation. Finish the current group before cancelling
            // the scan; abandoning the task here could leave writes running after its DI scope is disposed.
            var result = await action(argument).ConfigureAwait(false);
            if (result is not NoContentResult)
            {
                var status = (result as IStatusCodeActionResult)?.StatusCode;
                var detail = (result as ObjectResult)?.Value;
                throw new InvalidOperationException(
                    $"Jellyfin VideosController.{method.Name} failed: {status?.ToString() ?? result?.GetType().Name ?? "null result"}. {detail}");
            }
        }

        private static MethodInfo GetAction(Type controller, string name, Type parameter)
        {
            var method = controller.GetMethod(name, new[] { parameter });
            if (method == null || method.IsStatic || method.ReturnType != typeof(Task<ActionResult>))
            {
                throw new NotSupportedException(
                    $"The installed Jellyfin server has an incompatible VideosController.{name} signature.");
            }

            return method;
        }
    }
}
