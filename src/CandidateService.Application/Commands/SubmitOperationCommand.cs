using CandidateService.Application.DTOs;
using CandidateService.Application.Exceptions;
using CandidateService.Application.Interfaces;
using CandidateService.Domain.Entities;
using CandidateService.Domain.Enums;
using CandidateService.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CandidateService.Application.Commands
{
    public class SubmitOperationCommand : IRequest<SubmitOperationResponse>
    {
        public string OperationId { get; set; } = string.Empty;
    }

    public class SubmitOperationResponse
    {
        public OperationResponse Operation { get; set; } = new();
        public bool IsNew { get; set; }
    }

    public class SubmitOperationCommandHandler : IRequestHandler<SubmitOperationCommand, SubmitOperationResponse>
    {
        private readonly IOperationRepository _repository;
        private readonly ILogger<SubmitOperationCommandHandler> _logger;
        private readonly IBackgroundTaskQueue _backgroundTaskQueue;


        public SubmitOperationCommandHandler(
        IOperationRepository repository,
        IBackgroundTaskQueue backgroundTaskQueue,
        ILogger<SubmitOperationCommandHandler> logger)
        {
            _repository = repository;
            _backgroundTaskQueue = backgroundTaskQueue;
            _logger = logger;
        }

        public async Task<SubmitOperationResponse> Handle(SubmitOperationCommand request, CancellationToken cancellationToken)
        {
            var operation = await _repository.GetByIdAsync(request.OperationId);

            if (operation == null)
            {
                throw new NotFoundException($"Operation {request.OperationId} not found");
            }

            if (operation.Status == OperationStatus.CREATED)
            {
                operation.MarkAsProcessing();
                await _repository.UpdateAsync(operation);
                _backgroundTaskQueue.QueueBackgroundWorkItem(async (serviceProvider, token) =>
                {
                    await ProcessOperationAsync(serviceProvider, operation.Id, token);
                });

                _logger.LogInformation(
                "Operation submitted for processing. OperationId: {OperationId}",
                operation.Id
                );

                return new SubmitOperationResponse
                {
                    Operation = MapToResponse(operation),
                    IsNew = true
                };
            }

            _logger.LogInformation(
            "Duplicate submit request for operation {OperationId}. Current status: {Status}",
            operation.Id,
            operation.Status
            );

            return new SubmitOperationResponse
            {
                Operation = MapToResponse(operation),
                IsNew = false
            };
        }

        public async Task ProcessOperationAsync(IServiceProvider serviceProvider, string operationId, CancellationToken token)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<SubmitOperationCommandHandler>>();

            try
            {
                logger.LogInformation("=== START ProcessOperationAsync for {OperationId} ===", operationId);

                var repository = serviceProvider.GetRequiredService<IOperationRepository>();
                var providerService = serviceProvider.GetRequiredService<IProviderService>();
                var backgroundQueue = serviceProvider.GetRequiredService<IBackgroundTaskQueue>();

                logger.LogInformation("Getting operation {OperationId} from repository", operationId);
                var operation = await repository.GetByIdAsync(operationId);

                if (operation == null)
                {
                    logger.LogWarning("Operation {OperationId} not found", operationId);
                    return;
                }

                logger.LogInformation("Operation {OperationId} found. Status: {Status}, IsProcessing: {IsProcessing}",
                    operationId, operation.Status, operation.IsProcessing);

                if (operation.Status != OperationStatus.PROCESSING)
                {
                    logger.LogWarning("Operation {OperationId} is not in PROCESSING state. Current status: {Status}",
                        operationId, operation.Status);
                    return;
                }

                if (operation.IsProcessing)
                {
                    logger.LogWarning("Operation {OperationId} is already being processed", operationId);
                    return;
                }

                logger.LogInformation("Marking operation {OperationId} as processing", operationId);
                operation.IsProcessing = true;
                await repository.UpdateAsync(operation);

                logger.LogInformation("Sending request to provider for operation {OperationId}", operationId);
                var response = await providerService.SendPaymentAsync(operation);

                logger.LogInformation("Provider response received for operation {OperationId}. Success: {Success}, ProviderPaymentId: {ProviderPaymentId}, StatusCode: {StatusCode}",
                    operationId, response.Success, response.ProviderPaymentId, response.StatusCode);

                if (response.Success && !string.IsNullOrEmpty(response.ProviderPaymentId))
                {
                    logger.LogInformation("Provider accepted payment for operation {OperationId}. ProviderPaymentId: {ProviderPaymentId}",
                        operationId, response.ProviderPaymentId);

                    operation.ProviderPaymentId ??= response.ProviderPaymentId;
                    operation.ResetProcessing();
                    await repository.UpdateAsync(operation);
                    logger.LogInformation("Operation {OperationId} updated successfully", operationId);
                }
                else if (response.StatusCode == 0 || response.StatusCode >= 500)
                {
                    logger.LogWarning("Provider request failed for operation {OperationId}. Scheduling retry #{RetryCount}",
                        operationId, operation.RetryCount + 1);

                    operation.MarkRetryScheduled();
                    operation.ResetProcessing();
                    await repository.UpdateAsync(operation);

                    backgroundQueue.QueueBackgroundWorkItem(async (sp, ct) =>
                    {
                        using var taskScope = sp.CreateScope();
                        var handler = taskScope.ServiceProvider.GetRequiredService<SubmitOperationCommandHandler>();
                        await handler.ProcessOperationAsync(taskScope.ServiceProvider, operationId, ct);
                    });

                    logger.LogInformation("Retry scheduled for operation {OperationId} at {NextRetryAt}",
                        operationId, operation.NextRetryAt);
                }
                else
                {
                    logger.LogError("Provider returned client error for operation {OperationId}. Status: {StatusCode}",
                        operationId, response.StatusCode);

                    operation.ResetProcessing();
                    await repository.UpdateAsync(operation);
                }

                logger.LogInformation("=== END ProcessOperationAsync for {OperationId} ===", operationId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "=== ERROR in ProcessOperationAsync for {OperationId} ===", operationId);

                try
                {
                    var repository = serviceProvider.GetRequiredService<IOperationRepository>();
                    var backgroundQueue = serviceProvider.GetRequiredService<IBackgroundTaskQueue>();

                    var operation = await repository.GetByIdAsync(operationId);
                    if (operation != null)
                    {
                        logger.LogInformation("Recovering operation {OperationId} after error", operationId);
                        operation.ResetProcessing();
                        operation.MarkRetryScheduled();
                        await repository.UpdateAsync(operation);

                        backgroundQueue.QueueBackgroundWorkItem(async (sp, ct) =>
                        {
                            using var taskScope = sp.CreateScope();
                            var handler = taskScope.ServiceProvider.GetRequiredService<SubmitOperationCommandHandler>();
                            await handler.ProcessOperationAsync(taskScope.ServiceProvider, operationId, ct);
                        });
                    }
                }
                catch (Exception innerEx)
                {
                    logger.LogError(innerEx, "Failed to handle error for operation {OperationId}", operationId);
                }
            }
        }

        private OperationResponse MapToResponse(Operation operation)
        {
            return new OperationResponse
            {
                OperationId = operation.Id,
                Amount = operation.Amount.ToString("F2"),
                Currency = operation.Currency,
                Description = operation.Description,
                Status = operation.Status.ToString(),
                ProviderPaymentId = operation.ProviderPaymentId
            };
        }
    }
}
