using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Bullfrog.Common.Telemetry;
using Eshopworld.Core;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Monitor.Fluent;

namespace Bullfrog.Common.Helpers
{
    /// <summary>
    /// Estimates the size of VM scale set using its load balancer.
    /// </summary>
    public class ScaleSetMonitor
    {
        private readonly Azure.IAuthenticated _authenticated;
        private readonly IBigBrother _bigBrother;
        private IMetricDefinition _metricDefinition;

        public ScaleSetMonitor(Azure.IAuthenticated authenticated, IBigBrother bigBrother)
        {
            _authenticated = authenticated;
            _bigBrother = bigBrother;
        }

        /// <summary>
        /// Checks whether the necessary load balancer metrics exists and can be retrieved.
        /// </summary>
        /// <param name="configuration">The load balancer details.</param>
        /// <returns>Validation results.</returns>
        public virtual async Task<ValidationResult> ValidateAccess(LoadBalancerConfiguration configuration)
        {
            System.Collections.Generic.IReadOnlyList<IMetricDefinition> definitons;

            try
            {
                definitons = await GetMetricsDefinitions(configuration.LoadBalancerResourceId);
            }
            catch (Exception ex)
            {
                return new ValidationResult($"Cannot read scale set metric definition: {ex.Message}",
                    new[] { nameof(configuration.LoadBalancerResourceId) });
            }

            var metricDefinition = definitons.FirstOrDefault(x => x.Name.Value == "DipAvailability");
            if (metricDefinition == null)
                return new ValidationResult("Failed to find the DipAvailability metric definition.");

            var metricCollection = await ReadProbeMetrics(metricDefinition, configuration.HealthProbePort);
            if (metricCollection.Metrics.Count < 0)
                return new ValidationResult("Failed to read the DipAvailability metric.",
                    new[] { nameof(configuration.HealthProbePort) });

            if (metricCollection.Metrics[0].Timeseries.Count == 0)
                return new ValidationResult("The DipAvailability metric has no time series.",
                    new[] { nameof(configuration.HealthProbePort) });

            if (metricCollection.Metrics[0].Timeseries.Any(t => t.Data.Count == 0))
                return new ValidationResult("The DipAvailability metric contains empty time series.",
                    new[] { nameof(configuration.HealthProbePort) });

            return ValidationResult.Success;
        }

        /// <summary>
        /// Gets the number of active VMs in a VM scale set based on metrics of its load balancer.
        /// </summary>
        /// <param name="loadBalancerResourceId">The resource id of the load balancer.</param>
        /// <param name="healthProbePort">The port used by health probe.</param>
        /// <returns>Number of VM instances which responds at given port.</returns>

        public virtual async Task<int> GetNumberOfWorkingInstances(string loadBalancerResourceId, int healthProbePort)
        {
            try
            {
                if (_metricDefinition == null)
                {
                    var metrics = await _bigBrother.LogAzureCallDuration("ListMetrics", loadBalancerResourceId,
                        () => GetMetricsDefinitions(loadBalancerResourceId));

                    _metricDefinition = metrics.First(x => x.Name.Value == "DipAvailability");
                }

                var metricCollection = await _bigBrother.LogAzureCallDuration("ReadMetrics", loadBalancerResourceId, () =>
                    ReadProbeMetrics(_metricDefinition, healthProbePort));

                return metricCollection.Metrics[0].Timeseries.Count(t => t.Data.Last().Average > 95);
            }
            catch (Exception ex)
            {
                _bigBrother.Publish(ex.ToExceptionEvent());
                return 0;
            }
        }

        private static Task<Microsoft.Azure.Management.Monitor.Fluent.Models.IMetricCollection> ReadProbeMetrics(IMetricDefinition metricDefinition, int healthProbePort)
        {
            var recordDateTime = DateTime.UtcNow;
            return metricDefinition.DefineQuery()
                                    .StartingFrom(recordDateTime.AddMinutes(-1))
                                    .EndsBefore(recordDateTime)
                                    .WithAggregation("average")
                                    .WithInterval(TimeSpan.FromMinutes(1))
                                    .WithOdataFilter($"BackendPort eq '{healthProbePort}' and BackendIPAddress eq '*'")
                                    .ExecuteAsync();
        }

        private async Task<System.Collections.Generic.IReadOnlyList<IMetricDefinition>> GetMetricsDefinitions(string loadBalancerResourceId)
        {
            var azure = _authenticated.WithSubscriptionFor(loadBalancerResourceId);
            return await azure.MetricDefinitions.ListByResourceAsync(loadBalancerResourceId);
        }
    }
}
