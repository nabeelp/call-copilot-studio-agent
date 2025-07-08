// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CopilotStudioFunctionApp.Models;

public class SendMessageRequest
{
    public string Message { get; set; } = string.Empty;
    public string? ConversationId { get; set; }
    public bool OnlyReturnMessages { get; set; } = true;
}

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public string? Error { get; set; }
}

public class ConversationResponse
{
    public string? ConversationId { get; set; }
    public List<ActivityInfo> Activities { get; set; } = [];
}

public class MessageResponse
{
    public List<ActivityInfo> Activities { get; set; } = [];
}

public class ActivityInfo
{
    public string? Type { get; set; }
    public string? Text { get; set; }
    public string? TextFormat { get; set; }
    public IEnumerable<SuggestedActionInfo>? SuggestedActions { get; set; }
}

public class SuggestedActionInfo
{
    public string? Text { get; set; }
    public object? Value { get; set; }
}

public class HealthCheckResponse
{
    public string Status { get; set; } = "Healthy";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
