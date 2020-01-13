using System;
using System.Threading.Tasks;
using Bullfrog.Actors.EventModels;
using Bullfrog.Actors.Helpers;
using Bullfrog.Common;
using Bullfrog.Common.Helpers;
using Eshopworld.Telemetry;
using Bullfrog.Common.Telemetry;
using Eshopworld.Core;

namespace Bullfrog.Actors.ResourceScalers
{
    /// <summary>
    /// VM scale set scaler which uses Automation runbooks to modify autoscale settings.
    /// </summary>
    public class RunbookVmssScaler : ResourceScaler
    {
        private static readonly TimeSpan JobProcessingTimeout = TimeSpan.FromMinutes(5);
        private readonly RunbookVmssScalerConfiguration _configuration;
        private readonly IRunbookClient _runbookClient;
        private readonly ScaleSetMonitor _scaleSetMonitor;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IBigBrother _bigBrother;
        private readonly IAutoscaleSettingsHandler _autoscaleSettingsHandler;

        public RunbookVmssScaler(RunbookVmssScalerConfiguration configuration, IAutoscaleSettingsHandlerFactory autoscaleSettingsHandlerFactory, IRunbookClient runbookClient, ScaleSetMonitor scaleSetMonitor, IDateTimeProvider dateTimeProvider, IBigBrother bigBrother)
        {
            _configuration = configuration;
            _runbookClient = runbookClient;
            _scaleSetMonitor = scaleSetMonitor;
            _dateTimeProvider = dateTimeProvider;
            _bigBrother = bigBrother;
            _autoscaleSettingsHandler = autoscaleSettingsHandlerFactory.CreateHandler(
                _configuration.ScaleSet.AutoscaleSettingsResourceId,
                _configuration.ScaleSet.ProfileName);
        }

        public override async Task<bool> ScaleIn()
        {
            return await PerformOperationWithState<State, bool>(async state =>
            {
                if (!await HasPreviousJobCompleted(state))
                    return false;

                var autoscale = await _autoscaleSettingsHandler.Read();

                if (autoscale.BullfrogProfile != null)
                {
                    var now = _dateTimeProvider.UtcNow;
                    state.Job = await StartJob(0, now);
                    return false;
                }

                return true;
            });
        }

        public override async Task<int?> ScaleOut(int throughput, DateTimeOffset endsAt)
        {
            // remove fractions of a second to ensure the value is preserved when saved to or parsed from the autoscale setting file.
            endsAt = endsAt.Subtract(TimeSpan.FromTicks(endsAt.Ticks % TimeSpan.TicksPerSecond));

            return await PerformOperationWithState<State, int?>(async state =>
            {
                if (!await HasPreviousJobCompleted(state))
                    return null;

                // Calculate the number of required instances to handle the requested throughput.
                var scaleSetConfig = _configuration.ScaleSet;

                var instances = (int)(throughput + (scaleSetConfig.ReservedInstances + 1) * scaleSetConfig.RequestsPerInstance - 1)
                     / scaleSetConfig.RequestsPerInstance;

                if (instances < scaleSetConfig.MinInstanceCount)
                    instances = scaleSetConfig.MinInstanceCount.Value;

                // Make sure the required number of instances is not out of valid range defined by the default profile.
                var autoscale = await _autoscaleSettingsHandler.Read();
                if (instances > autoscale.DefaultMaximum)
                    instances = autoscale.DefaultMaximum;
                if (instances < autoscale.DefaultMinimum)
                    instances = autoscale.DefaultMinimum;

                // Start the runbook if Bullfrog's profile needs to be created or updated.
                if (autoscale.BullfrogProfile?.Minimum != instances || autoscale.BullfrogProfile?.Ends != endsAt)
                {
                    state.Job = await StartJob(instances, endsAt);
                    return null;
                }

                // Check the number of instances that resonds to the load balancer's probes.
                var workingInstances = await _bigBrother.LogAzureCallDuration("GetNumberOfInstances", scaleSetConfig.LoadBalancerResourceId,
                    async () => await _scaleSetMonitor.GetNumberOfWorkingInstances(
                        scaleSetConfig.LoadBalancerResourceId, scaleSetConfig.HealthPortPort));

                var usableInstances = Math.Max(workingInstances - scaleSetConfig.ReservedInstances, 0);
                var availableThroughput = (int)(usableInstances * scaleSetConfig.RequestsPerInstance);

                _bigBrother.Publish(new ScaleSetThroughput
                {
                    ScalerName = scaleSetConfig.Name,
                    RequestedThroughput = throughput,
                    RequiredInstances = instances,
                    ConfiguredInstances = autoscale.BullfrogProfile.Minimum,
                    WorkingInstances = workingInstances,
                    AvailableThroughput = availableThroughput,
                });

                // return the final available throughput or a notification that the scaling out has not yet completed.
                if (instances <= workingInstances)
                    return availableThroughput;
                else
                    return null;
            });
        }

        private async Task<JobDetails> StartJob(int instancesCount, DateTimeOffset tillWhen)
        {
            var scaleSetConf = _configuration.ScaleSet;
            var parameters = new RunbookJobCreationParameters
            {
                RunbookName = scaleSetConf.Runbook.RunbookName,
                AutomationAccountResourceId = scaleSetConf.AutoscaleSettingsResourceId,
            };

            parameters.RunbookParameters.Add("InstanceName", scaleSetConf.Runbook.ScaleSetName ?? scaleSetConf.Name);
            parameters.RunbookParameters.Add("InstancesCount", instancesCount);
            parameters.RunbookParameters.Add("UntilDateTime", tillWhen);

            var jobId = await _runbookClient.CreateJob(parameters);
            return new JobDetails
            {
                Id = jobId,
                Requested = instancesCount,
                StartedAt = _dateTimeProvider.UtcNow,
            };
        }

        /// <summary>
        /// Checks whether previously started job completed and updates the state.
        /// </summary>
        /// <param name="state">The state.</param>
        /// <returns>Returns true if the job has compled, false if it is still executing and the resources might be soon changed.</returns>
        private async Task<bool> HasPreviousJobCompleted(State state)
        {
            if (state.Job != null)
            {
                // A job has been started.
                var jobState = await _runbookClient.GetJobStatus(_configuration.AutomationAccountResourceId, state.Job.Id);
                if (jobState.Status == RunbookJobStatus.Processing)
                {
                    var expectedCompletionTime = state.Job.StartedAt.Add(JobProcessingTimeout);
                    if (expectedCompletionTime < _dateTimeProvider.UtcNow)
                    {
                        // Job should have completed by now. Log the issue and proceed as if the runbook completed (hoping that the job won't mess anything later).
                        _bigBrother.Publish(new JobProcessingTimeout
                        {
                            AutomationAccountResourceId = _configuration.AutomationAccountResourceId,
                            JobId = state.Job.Id,
                            JobStartTime = state.Job.StartedAt,
                            JobExpectedCompletionTime = expectedCompletionTime,
                            RunbookName = _configuration.ScaleSet.Runbook.RunbookName,
                            Vmss = _configuration.ScaleSet.Runbook.ScaleSetName ?? _configuration.ScaleSet.Name,
                        });
                        state.Job = null;
                    }
                    else
                    {
                        _bigBrother.Publish(new JobProcessingInProgress
                        {
                            AutomationAccountResourceId = _configuration.AutomationAccountResourceId,
                            JobId = state.Job.Id,
                            JobStartTime = state.Job.StartedAt,
                            JobExpectedCompletionTime = expectedCompletionTime,
                            RunbookName = _configuration.ScaleSet.Runbook.RunbookName,
                            Vmss = _configuration.ScaleSet.Runbook.ScaleSetName ?? _configuration.ScaleSet.Name,
                        });

                        return false;
                    }
                }
                else if (jobState.Status != RunbookJobStatus.Succeeded)
                {
                    _bigBrother.Publish(new JobFailed
                    {
                        AutomationAccountResourceId = _configuration.AutomationAccountResourceId,
                        JobId = state.Job.Id,
                        RunbookName = _configuration.ScaleSet.Runbook.RunbookName,
                        Vmss = _configuration.ScaleSet.Runbook.ScaleSetName ?? _configuration.ScaleSet.Name,
                        Exception = jobState.Exception,
                        Status = jobState.ReportedStatus,
                        ProvisioningState = jobState.ProvisioningState,
                    });
                    state.Job = null;
                    return true;
                }
            }

            return true;
        }

        private class State
        {
            public JobDetails Job { get; set; }
        }

        private class JobDetails
        {
            public Guid Id { get; set; }

            public DateTimeOffset StartedAt { get; set; }

            public int? Requested { get; set; }
        }
    }
}
