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
            _context.Operations.Update(operation);
            await _context.SaveChangesAsync();
        }
    }
}
