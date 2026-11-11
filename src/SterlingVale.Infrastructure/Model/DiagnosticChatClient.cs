using System.ClientModel;
using System.Text.Json;
using Azure;
using Azure.Identity;
using Microsoft.Extensions.AI;
using SterlingVale.AgentShared.Analysis;

namespace SterlingVale.Infrastructure.Model;

public sealed class DiagnosticChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        }
        catch (ClientResultException exception)
        {
            string? code = null;
            var content = exception.GetRawResponse()?.Content;
            if (content is not null)
            {
                try
                {
                    using var document = JsonDocument.Parse(content.ToString());
                    if (document.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object &&
                        error.TryGetProperty("code", out var value) && value.ValueKind == JsonValueKind.String)
                    {
                        code = value.GetString();
                    }
                }
                catch (JsonException)
                {
                    // Non-JSON error bodies are deliberately not copied into artifacts.
                }
            }

            throw new ModelRequestException(Describe(exception.GetType().Name, exception.Status, code), exception);
        }
        catch (RequestFailedException exception)
        {
            throw new ModelRequestException(Describe(exception.GetType().Name, exception.Status, exception.ErrorCode), exception);
        }
        catch (AuthenticationFailedException exception)
        {
            throw new ModelRequestException(new FailureInfo(exception.GetType().Name,
                "Azure identity authentication failed. Run az login in this terminal and verify the selected identity."), exception);
        }
    }

    public static FailureInfo Describe(string category, int status, string? errorCode)
    {
        // Only known public service codes are retained; arbitrary response text may echo secrets.
        string? code = errorCode switch
        {
            "PermissionDenied" or "Forbidden" or "Unauthorized" or "AuthenticationError" or
            "DeploymentNotFound" or "ResourceNotFound" or "NotFound" or "InvalidApiVersionParameter" or
            "InvalidRequest" or "InvalidArgument" or "BadRequest" or "invalid_request_error" or
            "unsupported_parameter" or "unsupported_value" or "OperationNotSupported" or
            "RateLimitExceeded" or "TooManyRequests" or "400" or "401" or "403" or "404" or "429" or "500" or "503" or "content_filter" => errorCode,
            null => null,
            _ => "UnrecognizedServiceError",
        };
        string guidance = status switch
        {
            401 => "Check Azure identity or API-key authentication.",
            403 => "Check the Cognitive Services OpenAI User role and resource networking policy.",
            404 => "Check the base resource endpoint and exact deployment name.",
            429 => "Check quota and rate limits before retrying.",
            400 => "Check endpoint format, deployment/API compatibility and supported request parameters.",
            _ => "Check endpoint configuration, authentication and service availability.",
        };
        return new FailureInfo(category, $"Model request failed: HTTP {status}, error code {code ?? "unavailable"}. {guidance}", status, code);
    }
}
