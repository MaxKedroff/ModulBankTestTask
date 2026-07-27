using CandidateService.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CandidateService.Infrastructure.Services
{
    public class BackgroundTaskService : BackgroundService
    {
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BackgroundTaskService> _logger;

        public BackgroundTaskService(
            IBackgroundTaskQueue taskQueue,
            IServiceProvider serviceProvider,
            ILogger<BackgroundTaskService> logger)
        {
            _taskQueue = taskQueue;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("BackgroundTaskService is starting");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogDebug("Waiting for work item...");
                    var workItem = await _taskQueue.DequeueAsync(stoppingToken);

                    if (workItem != null)
                    {
                        _logger.LogInformation("=== WORK ITEM RECEIVED ===");
                        _logger.LogDebug("Executing background task");

                        try
                        {
                            using var scope = _serviceProvider.CreateScope();
                            _logger.LogDebug("Scope created, executing work item");
                            await workItem(scope.ServiceProvider, stoppingToken);
                            _logger.LogDebug("Background task completed successfully");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error executing background task");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("BackgroundTaskService is stopping due to cancellation");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in background task loop");
                    await Task.Delay(1000, stoppingToken);
                }
            }

            _logger.LogInformation("BackgroundTaskService is stopping");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("BackgroundTaskService is stopping gracefully");
            await base.StopAsync(cancellationToken);
        }
    }
}
