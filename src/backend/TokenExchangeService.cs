using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;

namespace CopilotStudioFunctionApp
{
    /// <summary>
    /// Handles exchanging an incoming access token for a new access token with the required scopes using the On-Behalf-Of (OBO) flow.
    /// </summary>
    public class TokenExchangeService
    {
        private readonly string[] _scopes;
        private readonly ILogger<TokenExchangeService> _logger;
        private readonly IConfidentialClientApplication _confidentialClient;

        public TokenExchangeService(IConfiguration config, ILogger<TokenExchangeService> logger)
        {
            var settings = config.GetSection("CopilotStudioClientSettings");
            var clientId = settings["AppClientId"] ?? throw new ArgumentNullException(nameof(config), "CopilotStudioClientSettings__AppClientId is required");
            var tenantId = settings["TenantId"] ?? throw new ArgumentNullException(nameof(config), "CopilotStudioClientSettings__TenantId is required");
            var clientSecret = settings["AppClientSecret"] ?? throw new ArgumentNullException(nameof(config), "CopilotStudioClientSettings__AppClientSecret is required");

            var scope = CopilotClient.ScopeFromCloud(Microsoft.Agents.CopilotStudio.Client.Discovery.PowerPlatformCloud.Prod)
                ?? throw new InvalidOperationException("ScopeFromCloud returned null.");
            _scopes = [scope];
            _logger = logger;

            _confidentialClient = ConfidentialClientApplicationBuilder
                .Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority($"https://login.microsoftonline.com/{tenantId}")
                .Build();
        }

        /// <summary>
        /// Exchanges the incoming access token for a new access token with the required scopes.
        /// </summary>
        /// <param name="incomingAccessToken">The incoming access token from the user</param>
        /// <returns>The new access token for Copilot Studio agent</returns>
        public async Task<string?> ExchangeTokenAsync(string incomingAccessToken)
        {
            try
            {
                var userAssertion = new UserAssertion(incomingAccessToken);
                var result = await _confidentialClient.AcquireTokenOnBehalfOf(_scopes, userAssertion).ExecuteAsync();
                _logger.LogInformation("Token exchange successful. New access token acquired.");

                return result.AccessToken;
            }
            catch (MsalServiceException ex)
            {
                _logger.LogError(ex, "MSAL service error during token exchange");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during token exchange");
                return null;
            }
        }
    }
}
