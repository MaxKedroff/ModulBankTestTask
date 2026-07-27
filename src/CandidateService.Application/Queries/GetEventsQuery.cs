using CandidateService.Application.DTOs;
using CandidateService.Application.Exceptions;
using CandidateService.Domain.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CandidateService.Application.Queries
{
    public class GetEventsQuery : IRequest<IEnumerable<EventResponse>>
    {
        public string OperationId { get; set; } = string.Empty;
    }

    public class GetEventsQueryHandler : IRequestHandler<GetEventsQuery, IEnumerable<EventResponse>>
    {
        private readonly IOperationRepository _repository;

        public GetEventsQueryHandler(IOperationRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<EventResponse>> Handle(GetEventsQuery request, CancellationToken cancellationToken)
        {
            var operation = await _repository.GetByIdAsync(request.OperationId);

            if (operation == null)
            {
                throw new NotFoundException($"Operation {request.OperationId} not found");
            }

            return operation.Events.Select(e => new EventResponse
            {
                EventId = e.EventId,
                Type = e.Type,
                FromStatus = e.FromStatus,
                ToStatus = e.ToStatus,
                Message = e.Message,
                OccurredAt = e.OccurredAt
            });
        }
    }
}
