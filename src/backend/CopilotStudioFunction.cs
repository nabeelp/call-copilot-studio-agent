// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Agents.Core.Models;
using System.Net;
using System.Text.Json;
using CopilotStudioClientSample;
using CopilotStudioFunctionApp.Models;

namespace CopilotStudioFunctionApp;

public class CopilotStudioFunction
{
    private readonly ILogger<CopilotStudioFunction> _logger;
    private readonly CopilotClient _copilotClient;
    private readonly TokenExchangeService _tokenExchangeService;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public CopilotStudioFunction(
        ILogger<CopilotStudioFunction> logger,
        CopilotClient copilotClient,
        TokenExchangeService tokenExchangeService)
    {
        _logger = logger;
        _copilotClient = copilotClient;
        _tokenExchangeService = tokenExchangeService;
    }

    /// <summary>
    /// Extracts the bearer token from the Authorization header of the incoming request.
    /// </summary>
    /// <param name="req">The HTTP request</param>
    /// <returns>The bearer token or null if not found</returns>
    private static string? ExtractBearerToken(HttpRequestData req)
    {
        if (!req.Headers.TryGetValues("Authorization", out var authHeaders))
            return null;

        var authHeader = authHeaders.FirstOrDefault();
        return !string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authHeader["Bearer ".Length..].Trim()
            : null;
    }

    /// <summary>
    /// Creates a standardized error response.
    /// </summary>
    private static HttpResponseData CreateErrorResponse(HttpRequestData req, HttpStatusCode statusCode, string message, string error = "")
    {
        var response = req.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        
        var errorResult = new ApiResponse<object>
        {
            Success = false,
            Message = message,
            Error = string.IsNullOrEmpty(error) ? statusCode.ToString() : error
        };
        
        response.WriteString(JsonSerializer.Serialize(errorResult, JsonOptions));
        return response;
    }

    /// <summary>
    /// Creates a standardized success response.
    /// </summary>
    private static async Task<HttpResponseData> CreateSuccessResponseAsync<T>(HttpRequestData req, string message, T data)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        
        var result = new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data
        };
        
        await response.WriteStringAsync(JsonSerializer.Serialize(result, JsonOptions));
        return response;
    }

    /// <summary>
    /// Maps an Activity to a simplified object for JSON serialization.
    /// </summary>
    private static ActivityInfo MapActivity(Activity activity) => new()
    {
        Type = activity.Type,
        Text = activity.Text,
        TextFormat = activity.TextFormat,
        SuggestedActions = activity.SuggestedActions?.Actions?.Select(a => new SuggestedActionInfo { Text = a.Text, Value = a.Value })
    };

    /// <summary>
    /// Validates that the request contains a valid bearer token and exchanges it for a new token with the required scopes.
    /// </summary>
    /// <param name="req">The HTTP request</param>
    /// <returns>Unauthorized response if token is missing or exchange fails, otherwise null</returns>
    private async Task<HttpResponseData?> ValidateAuthenticationAsync(HttpRequestData req)
    {
        var token = ExtractBearerToken(req);
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Request missing or invalid Authorization header");
            return CreateErrorResponse(req, HttpStatusCode.Unauthorized, "Authorization header with Bearer token is required");
        }

        // Exchange the incoming token for a new one with the required scopes
        var exchangedToken = await _tokenExchangeService.ExchangeTokenAsync(token);
        if (string.IsNullOrEmpty(exchangedToken))
        {
            _logger.LogWarning("Token exchange failed or returned null");
            return CreateErrorResponse(req, HttpStatusCode.Unauthorized, "Token exchange failed. Access denied.");
        }

        // Set the exchanged token for the PassThroughTokenHandler to use
        PassThroughTokenHandler.SetCurrentToken(exchangedToken);
        return null;
    }

    [Function("StartConversation")]
    public async Task<HttpResponseData> StartConversation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        _logger.LogInformation("Starting new conversation with Copilot Studio");

        // Validate authentication and exchange token
        var authResponse = await ValidateAuthenticationAsync(req);
        if (authResponse != null)
            return authResponse;

        try
        {
            var activities = new List<ActivityInfo>();
            var conversationId = string.Empty;

            // loop over the activities returned by the CopilotClient
            // and add them to the activities list
            await foreach (Activity activity in _copilotClient.StartConversationAsync(emitStartConversationEvent: true))
            {
                if (activity != null)
                {
                    conversationId = activity.Conversation?.Id;
                    activities.Add(MapActivity(activity));
                }
            }

            var result = new ConversationResponse
            {
                ConversationId = conversationId,
                Activities = activities
            };

            return await CreateSuccessResponseAsync(req, "Conversation started successfully", result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting conversation");
            return CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Error starting conversation", ex.Message);
        }
        finally
        {
            // Clear the token after use
            PassThroughTokenHandler.ClearCurrentToken();
        }
    }

    [Function("SendMessage")]
    public async Task<HttpResponseData> SendMessage(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        _logger.LogInformation("Processing message request");

        // Validate authentication and exchange token
        var authResponse = await ValidateAuthenticationAsync(req);
        if (authResponse != null)
            return authResponse;

        try
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var requestData = JsonSerializer.Deserialize<SendMessageRequest>(requestBody, JsonOptions);

            if (string.IsNullOrEmpty(requestData?.Message) || string.IsNullOrEmpty(requestData?.ConversationId))
            {
                return CreateErrorResponse(req, HttpStatusCode.BadRequest, "Message and ConversationId are required");
            }

            var activities = new List<ActivityInfo>();

            await foreach (Activity activity in _copilotClient.AskQuestionAsync(requestData.Message, requestData.ConversationId))
            {
                if (activity != null)
                {
                    if (requestData.OnlyReturnMessages && activity.Type != "message")
                    {
                        continue; // Skip non-message activities if only returning messages
                    }

                    activities.Add(MapActivity(activity));
                }
            }

            var result = new MessageResponse
            {
                Activities = activities
            };

            return await CreateSuccessResponseAsync(req, "Message processed successfully", result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            return CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Error processing message", ex.Message);
        }
        finally
        {
            // Clear the token after use
            PassThroughTokenHandler.ClearCurrentToken();
        }
    }

    [Function("HealthCheck")]
    public async Task<HttpResponseData> HealthCheck(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Health check requested");

        var result = new HealthCheckResponse();

        return await CreateSuccessResponseAsync(req, "Health check completed", result);
    }
}
