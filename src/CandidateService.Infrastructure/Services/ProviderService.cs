using CandidateService.Domain.Entities;
using CandidateService.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Polly;
using System.Text.Json;
using System.Text;

namespace CandidateService.Infrastructure.Services
{
    public class ProviderService : IProviderService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ProviderService> _logger;
        private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

        public ProviderService(HttpClient httpClient, ILogger<ProviderService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            _retryPolicy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(r => r.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            .WaitAndRetryAsync(
                retryCount: 5,
                sleepDurationProvider: (retryAttempt) =>
                {
                    var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                    var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500));
                    return baseDelay.Add(jitter);
                },
                onRetry: (response, timeSpan, retryCount, context) =>
                {
                    var operationId = context["operationId"]?.ToString() ?? "unknown";
                    _logger.LogWarning(
                        "Retry {RetryCount} for operation {OperationId} after {Delay}ms. Status: {Status}",
                        retryCount,
                        operationId,
                        timeSpan.TotalMilliseconds,
                        response.Result?.StatusCode ?? 0
                    );
                }
            );
        }

        public async Task<ProviderPaymentResponse> SendPaymentAsync(Operation operation)
        {
            var request = new
            {
                operationId = operation.Id,
                amount = operation.Amount.ToString("F2"),
                currency = operation.Currency
            };

            var content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json"
                );

            var context = new Context
            {
                ["operationId"] = operation.Id
            };

            try
            {
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, "")
                {
                    Content = content
                };

                requestMessage.Headers.Add("Idempotency-Key", operation.Id);
                requestMessage.Headers.Add("X-Correlation-ID", operation.Id);

                _logger.LogInformation(
                    "Sending payment request to provider. OperationId: {OperationId}, Amount: {Amount}",
                    operation.Id,
                    operation.Amount
                );

                var response = await _retryPolicy.ExecuteAsync(
                    (ctx, token) => _httpClient.SendAsync(requestMessage, token),
                    context,
                    CancellationToken.None
                );

                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var providerResponse = JsonSerializer.Deserialize<ProviderResponse>(responseContent);

                        _logger.LogInformation(
                       "Provider accepted payment. OperationId: {OperationId}, ProviderPaymentId: {ProviderPaymentId}",
                       operation.Id,
                       providerResponse?.ProviderPaymentId
                        );

                        return new ProviderPaymentResponse
                        {
                            Success = true,
                            ProviderPaymentId = providerResponse?.ProviderPaymentId,
                            StatusCode = (int)response.StatusCode
                        };
                    } catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Failed to parse provider response for operation {OperationId}", operation.Id);
                        return new ProviderPaymentResponse
                        {
                            Success = false,
                            ErrorMessage = "Invalid provider response format",
                            StatusCode = (int)response.StatusCode
                        };
                    }
                }
                else
                {
                    _logger.LogWarning(
                   "Provider returned error status. OperationId: {OperationId}, StatusCode: {StatusCode}, Response: {Response}",
                   operation.Id,
                   (int)response.StatusCode,
                   responseContent
                    );

                    return new ProviderPaymentResponse
                    {
                        Success = false,
                        ErrorMessage = $"Provider error: {response.StatusCode}",
                        StatusCode = (int)response.StatusCode
                    };
                }
            } catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending payment to provider for operation {OperationId}", operation.Id);

                return new ProviderPaymentResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    StatusCode = 0
                };
            }
        }

        private class ProviderResponse
        {
            public string? ProviderPaymentId { get; set; }
            public string? Status { get; set; }
        }
    }
}
