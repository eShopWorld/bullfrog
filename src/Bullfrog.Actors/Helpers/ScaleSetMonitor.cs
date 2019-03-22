using System;
using System.Linq;
using System.Threading.Tasks;
using Bullfrog.Actors.EventModels;
using Eshopworld.Core;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Monitor.Fluent;

namespace Bullfrog.Actors.Helpers
{
    class ScaleSetMonitor : IScaleSetMonitor
    {
        private readonly IAzure _azure;
        private readonly IBigBrother _bigBrother;
        private IMetricDefinition _metricDefinition;

        public ScaleSetMonitor(IAzure azure, IBigBrother bigBrother)
        {
            _azure = azure;
            _bigBrother = bigBrother;
        }

        public async Task<int> GetNumberOfWorkingInstances(string loadBalancerResourceId, int healthProbePort)
        {
            try
            {
                if (_metricDefinition == null)
                {
                    var listResourcesOperation = new AzureOperationDurationEvent
                    {
                        Operation = "ListMetrics",
                        ResourceId = loadBalancerResourceId,
                    };
                    var metrics = await _azure.MetricDefinitions.ListByResourceAsync(loadBalancerResourceId);
                    _bigBrother.Publish(listResourcesOperation);

                    _metricDefinition = metrics.First(x => x.Name.Value == "DipAvailability");
                }

                var readMetricsOperation = new AzureOperationDurationEvent
                {
                    Operation = "ReadMetrics",
                    ResourceId = loadBalancerResourceId,
                };
                DateTime recordDateTime = DateTime.UtcNow;
                var metricCollection = _metricDefinition.DefineQuery()
                        .StartingFrom(recordDateTime.AddMinutes(-1))
                        .EndsBefore(recordDateTime)
                        .WithAggregation("average")
                        .WithInterval(TimeSpan.FromMinutes(1))
                        .WithOdataFilter($"BackendPort eq '{healthProbePort}' and BackendIPAddress eq '*'")
                        .Execute();
                _bigBrother.Publish(readMetricsOperation);

                return metricCollection.Metrics[0].Timeseries.Count(t => t.Data.Last().Average > 95);
            }
            catch (Exception ex)
            {
                _bigBrother.Publish(ex.ToExceptionEvent());
                return 0;
            }
        }
    }
}
