using CandidateService.Domain.Entities;
using CandidateService.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CandidateService.Infrastructure.Data
{
    public class OperationRepository : IOperationRepository
    {
        private readonly ApplicationDbContext _context;

        public OperationRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Operation> CreateAsync(Operation operation)
        {
            await _context.Operations.AddAsync(operation);
            await _context.SaveChangesAsync();
            return operation;
        }

        public async Task<bool> ExistsAsync(string id)
        {
            return await _context.Operations.AnyAsync(o => o.Id == id);
        }

        public async Task<Operation?> GetByIdAsync(string id)
        {
            return await _context.Operations
                .Include(o => o.Events)
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        public async Task<IEnumerable<OperationEvent>> GetEventsAsync(string operationId)
        {
            var operation = await GetByIdAsync(operationId);
            return operation?.Events ?? new List<OperationEvent>();
        }

        public async Task<IEnumerable<Operation>> GetProcessingOperationsAsync()
        {
            return await _context.Operations
                .Include(o => o.Events)
                .Where(o => o.Status == Domain.Enums.OperationStatus.PROCESSING)
                .ToListAsync();
        }

        public async Task UpdateAsync(Operation operation)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var existing = await _context.Operations
                    .Include(o => o.Events)
                    .FirstOrDefaultAsync(o => o.Id == operation.Id);


                if (existing == null)
                {
                    throw new InvalidOperationException($"Operation {operation.Id} not found");
                }

                var existingEventIds = existing.Events.Select(e => e.EventId).ToHashSet();
                var newEvents = operation.Events
                    .Where(e => !existingEventIds.Contains(e.EventId))
                    .ToList();

                if (newEvents.Any())
                {
                    var nextEventId = existingEventIds.Any() ? existingEventIds.Max() + 1 : 1;

                    while (existingEventIds.Contains(nextEventId))
                    {
                        nextEventId++;
                    }

                    foreach (var newEvent in newEvents)
                    {
                        if (existingEventIds.Contains(newEvent.EventId))
                        {
                            newEvent.EventId = nextEventId;
                            nextEventId++;
                        }
                        else
                        {
                            if (newEvents.Any(e => e != newEvent && e.EventId == newEvent.EventId))
                            {
                                newEvent.EventId = nextEventId;
                                nextEventId++;
                            }
                        }

                        existing.Events.Add(newEvent);
                        existingEventIds.Add(newEvent.EventId);
                    }
                }

                existing.Status = operation.Status;
                existing.ProviderPaymentId = operation.ProviderPaymentId;
                existing.IsProcessing = operation.IsProcessing;
                existing.UpdatedAt = operation.UpdatedAt;
                existing.RetryCount = operation.RetryCount;
                existing.NextRetryAt = operation.NextRetryAt;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _context.Entry(operation).CurrentValues.SetValues(existing);
                operation.Events.Clear();
                foreach (var evt in existing.Events)
                {
                    operation.Events.Add(evt);
                }
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
