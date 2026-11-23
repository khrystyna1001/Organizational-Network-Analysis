using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using ONA.Insights.Infrastructure.Auth;

namespace ONA.Insights.Infrastructure.Acl;

/// <summary>
/// Authenticates calls to Analysis: forwards the caller's bearer token when there is an HTTP request,
/// otherwise (background consumer) uses a short-lived service token.
/// </summary>
internal sealed class BearerForwardingHandler(IHttpContextAccessor accessor, ServiceTokenFactory serviceTokens)
    : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var incoming = accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(incoming))
        {
            request.Headers.Remove("Authorization");
            request.Headers.TryAddWithoutValidation("Authorization", incoming);
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceTokens.Create());
        }

        return base.SendAsync(request, cancellationToken);
    }
}
