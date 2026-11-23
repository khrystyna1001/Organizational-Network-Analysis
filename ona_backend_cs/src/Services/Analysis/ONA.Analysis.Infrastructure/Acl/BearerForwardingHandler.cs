using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;

namespace ONA.Analysis.Infrastructure.Acl;

/// <summary>
/// Authenticates outgoing calls to Graph: forwards the caller's bearer token when there is an
/// incoming HTTP request, otherwise (background consumer) uses a minted service token.
/// </summary>
internal sealed class BearerForwardingHandler(
    IHttpContextAccessor httpContextAccessor,
    ServiceTokenFactory serviceTokens) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var incoming = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        request.Headers.Authorization =
            !string.IsNullOrWhiteSpace(incoming) && AuthenticationHeaderValue.TryParse(incoming, out var forwarded)
                ? forwarded
                : new AuthenticationHeaderValue("Bearer", serviceTokens.Create());
        return base.SendAsync(request, cancellationToken);
    }
}
