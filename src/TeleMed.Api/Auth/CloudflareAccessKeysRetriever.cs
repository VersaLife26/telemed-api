using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace TeleMed.Api.Auth;

// Cloudflare Access publishes a bare JWKS rather than an OpenID discovery document, so JwtBearer's
// ConfigurationManager (key caching and refresh-on-unknown-kid) needs this adapter.
internal sealed class CloudflareAccessKeysRetriever(string issuer) : IConfigurationRetriever<OpenIdConnectConfiguration>
{
    public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(string address, IDocumentRetriever retriever, CancellationToken cancel)
    {
        var keys = new JsonWebKeySet(await retriever.GetDocumentAsync(address, cancel));
        var configuration = new OpenIdConnectConfiguration { Issuer = issuer, JwksUri = address, JsonWebKeySet = keys };
        foreach (var key in keys.GetSigningKeys())
        {
            configuration.SigningKeys.Add(key);
        }

        return configuration;
    }
}
