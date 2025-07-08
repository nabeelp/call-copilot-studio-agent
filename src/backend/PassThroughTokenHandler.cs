// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http.Headers;

namespace CopilotStudioClientSample
{
    /// <summary>
    /// This token handler will be set up to receive the bearer token from the 
    /// function context and pass it through to the Copilot Studio client.
    /// </summary>
    internal class PassThroughTokenHandler : DelegatingHandler
    {
        private static readonly ThreadLocal<string?> _currentToken = new ThreadLocal<string?>();

        public PassThroughTokenHandler() : base(new HttpClientHandler())
        {
        }

        /// <summary>
        /// Sets the current token to be used for outgoing requests.
        /// This should be called from the Azure Function before making any calls.
        /// </summary>
        /// <param name="token">The bearer token to use</param>
        public static void SetCurrentToken(string? token)
        {
            _currentToken.Value = token;
        }

        /// <summary>
        /// Clears the current token after use.
        /// </summary>
        public static void ClearCurrentToken()
        {
            _currentToken.Value = null;
        }

        /// <summary>
        /// Applies the current token to outgoing requests to Copilot Studio.
        /// </summary>
        /// <param name="request">The outgoing HTTP request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The HTTP response</returns>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Only add authorization if not already present
            if (request.Headers.Authorization is null && !string.IsNullOrEmpty(_currentToken.Value))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _currentToken.Value);
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
