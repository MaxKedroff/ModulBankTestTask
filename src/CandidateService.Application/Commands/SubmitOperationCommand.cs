using CandidateService.Application.DTOs;
using CandidateService.Application.Exceptions;
using CandidateService.Application.Interfaces;
using CandidateService.Domain.Entities;
using CandidateService.Domain.Enums;
using CandidateService.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

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
            using var scope = serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IOperationRepository>();
            var providerService = scope.ServiceProvider.GetRequiredService<IProviderService>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SubmitOperationCommandHandler>>();
            try
            {
                var operation = await repository.GetByIdAsync(operationId);
                if (operation == null || operation.Status != OperationStatus.PROCESSING)
                {
                    return;
                }

                if (operation.IsProcessing)
                {
                    logger.LogWarning("Operation {OperationId} is already being processed", operationId);
                    return;
                }

                operation.IsProcessing = true;
                await repository.UpdateAsync(operation);
                var response = await providerService.SendPaymentAsync(operation);
                if (response.Success && !string.IsNullOrEmpty(response.ProviderPaymentId))
                {
                    operation.ProviderPaymentId ??= response.ProviderPaymentId;
                    operation.ResetProcessing();
                    await repository.UpdateAsync(operation);
                    logger.LogInformation(
                    "Provider accepted payment for operation {OperationId}. ProviderPaymentId: {ProviderPaymentId}",
                    operationId,
                    response.ProviderPaymentId
                    );
                }
                else if (response.StatusCode == 0 || response.StatusCode >= 500)
                {
                    operation.MarkRetryScheduled();
                    operation.ResetProcessing();
                    await repository.UpdateAsync(operation);

                    _backgroundTaskQueue.QueueBackgroundWorkItem(async (sp, ct) =>
                    {
                        await ProcessOperationAsync(sp, operationId, ct);
                    });

                    logger.LogWarning(
                    "Provider request failed for operation {OperationId}. Scheduled retry #{RetryCount} at {NextRetryAt}",
                    operationId,
                    operation.RetryCount,
                    operation.NextRetryAt
                    );
                }
                else
                {
                    operation.ResetProcessing();
                    await repository.UpdateAsync(operation);
                    logger.LogError(
                    "Provider returned client error for operation {OperationId}. Status: {StatusCode}",
                    operationId,
                    response.StatusCode
                    );
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing operation {OperationId}", operationId);
                try
                {
                    var operation = await repository.GetByIdAsync(operationId);
                    if (operation != null)
                    {
                        operation.ResetProcessing();
                        operation.MarkRetryScheduled();
                        await repository.UpdateAsync(operation);

                        _backgroundTaskQueue.QueueBackgroundWorkItem(async (sp, ct) =>
                        {
                            await ProcessOperationAsync(sp, operationId, ct);
                        });
                    }
                }catch (Exception innerEx)
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
