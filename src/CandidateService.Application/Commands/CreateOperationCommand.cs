using CandidateService.Application.DTOs;
using MediatR;
using FluentValidation;
using CandidateService.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using CandidateService.Domain.Entities;
using CandidateService.Application.Exceptions;


namespace CandidateService.Application.Commands
{
    public class CreateOperationCommand : IRequest<OperationResponse>
    {
        public string OperationId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class CreateOperationCommandValidator : AbstractValidator<CreateOperationCommand>
    {
        public CreateOperationCommandValidator()
        {
            RuleFor(x => x.OperationId)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(x => x.Amount)
                .GreaterThan(0)
                .PrecisionScale(18, 2, true);

            RuleFor(x => x.Currency)
                .NotEmpty()
                .MaximumLength(3)
                .Must(c => c == "RUB")
                .WithMessage("Only RUB currency is supported");

            RuleFor(x => x.Description)
                .MaximumLength(500);
        }

        public class CreateOperationCommandHandler : IRequestHandler<CreateOperationCommand, OperationResponse>
        {
            private readonly IOperationRepository _repository;
            private readonly ILogger<CreateOperationCommandHandler> _logger;

            public CreateOperationCommandHandler(IOperationRepository repository, ILogger<CreateOperationCommandHandler> logger)
            {
                _repository = repository;
                _logger = logger;
            }

            public async Task<OperationResponse> Handle(CreateOperationCommand request, CancellationToken cancellationToken)
            {
                if (await _repository.ExistsAsync(request.OperationId))
                {
                    throw new ConflictException($"Operation with ID {request.OperationId} already exists");
                }

                var operation = new Operation(
                    request.OperationId,
                    request.Amount,
                    request.Currency,
                    request.Description
                );

                await _repository.CreateAsync(operation);
                _logger.LogInformation(
                    "Created new operation. OperationId: {OperationId}, Amount: {Amount}",
                    operation.Id,
                    operation.Amount
                );

                return MapToResponse(operation);
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
}
