using App.Metrics;
using App.Metrics.Counter;
using App.Metrics.Gauge;
using CandidateService.Application.Interfaces;

namespace CandidateService.Infrastructure.Services
{
    public class MetricsService : IMetricsService
    {
        private readonly IMetrics _metrics;

        public MetricsService(IMetrics metrics)
        {
            _metrics = metrics;
        }

        public void IncrementOperationCompleted()
        {
            _metrics.Measure.Counter.Increment(new CounterOptions
            {
                Name = "operations_completed",
                MeasurementUnit = Unit.Calls
            });
        }

        public void IncrementOperationCreated()
        {
            _metrics.Measure.Counter.Increment(new CounterOptions
            {
                Name = "operations_created",
                MeasurementUnit = Unit.Calls
            });
        }

        public void IncrementOperationRejected()
        {
            _metrics.Measure.Counter.Increment(new CounterOptions
            {
                Name = "operations_rejected",
                MeasurementUnit = Unit.Calls
            });
        }

        public void IncrementOperationSubmitted()
        {
            _metrics.Measure.Counter.Increment(new CounterOptions
            {
                Name = "operations_submitted",
                MeasurementUnit = Unit.Calls
            });
        }

        public void IncrementProviderRetry()
        {
            _metrics.Measure.Counter.Increment(new CounterOptions
            {
                Name = "provider_retries",
                MeasurementUnit = Unit.Calls
            });
        }

        public void SetPendingOperations(int count)
        {
            _metrics.Measure.Gauge.SetValue(new GaugeOptions
            {
                Name = "pending_operations",
                MeasurementUnit = Unit.Items
            }, () => count);
        }
    }
}
