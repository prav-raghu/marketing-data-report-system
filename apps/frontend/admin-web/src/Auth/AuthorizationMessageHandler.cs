using System.Net;
using System.Net.Http.Headers;

namespace AdminWeb.Auth;

public sealed class AuthorizationMessageHandler : DelegatingHandler
{
    private readonly AuthTokenStore _tokenStore;

    public AuthorizationMessageHandler(AuthTokenStore tokenStore) => _tokenStore = tokenStore;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_tokenStore.AccessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _tokenStore.Clear();
        }

        return response;
    }
}
