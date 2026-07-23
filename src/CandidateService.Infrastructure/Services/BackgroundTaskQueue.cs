using CandidateService.Application.Interfaces;
using System.Collections.Concurrent;

namespace CandidateService.Infrastructure.Services
{
    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private readonly ConcurrentQueue<Func<IServiceProvider, CancellationToken, Task>> _workItems = new();

        private readonly SemaphoreSlim _signal = new(0);


        public async Task<Func<IServiceProvider, CancellationToken, Task>?> DequeueAsync(CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken);
            _workItems.TryDequeue(out var workItem);
            return workItem;
        }

        public void QueueBackgroundWorkItem(Func<IServiceProvider, CancellationToken, Task> workItem)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            _workItems.Enqueue(workItem);
            _signal.Release();
        }
    }
}
