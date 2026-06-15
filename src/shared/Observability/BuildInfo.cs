using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AgenticTradersDesk.Observability;

/// <summary>
/// Build provenance baked into the container image at `docker build` time via the
/// BUILD_GIT_SHA / BUILD_TIME args (see each service Dockerfile and tasks/Taskfile.build.yml).
/// Lets a running container self-report exactly which committed-and-pushed source it was built
/// from, so "what is running in Azure" can be matched against `git rev-parse` in the repo.
/// </summary>
public static class BuildInfo
{
    /// <summary>Short git SHA the image was built from, or "unknown" for un-stamped local builds.</summary>
    public static string GitSha { get; } =
        Environment.GetEnvironmentVariable("BUILD_GIT_SHA") is { Length: > 0 } s ? s : "unknown";

    /// <summary>UTC build timestamp the image was built at, or "unknown".</summary>
    public static string BuildTime { get; } =
        Environment.GetEnvironmentVariable("BUILD_TIME") is { Length: > 0 } t ? t : "unknown";

    /// <summary>
    /// Map GET /version and GET /api/version returning the build provenance as JSON. The
    /// /api/version alias is reachable through the ui-app nginx /api/ reverse proxy, so the
    /// provenance of an internal-only service can be audited from the public UI origin.
    /// </summary>
    public static IEndpointRouteBuilder MapVersionEndpoints(this IEndpointRouteBuilder endpoints, string serviceName)
    {
        var payload = new { service = serviceName, sha = GitSha, buildTime = BuildTime };
        endpoints.MapGet("/version", () => Results.Json(payload));
        endpoints.MapGet("/api/version", () => Results.Json(payload));
        return endpoints;
    }
}
