using System;
using System.Threading.Tasks;
using Bullfrog.Actors.EventModels;
using Bullfrog.Actors.Interfaces;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.Actors.Models;
using Bullfrog.Actors.ResourceScalers;
using Bullfrog.Common;
using Eshopworld.Core;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;

namespace Bullfrog.Actors
{
    /// <summary>
    /// The actor responsible for scaling a single resource.
    /// </summary>
    public class ResourceScalingActor : BullfrogActorBase, IResourceScalingActor, IRemindable
    {
        /// <summary>
        /// The delay after which the requested operation is started.
        /// </summary>
        internal static TimeSpan OperationStartDelay { get; set; } = TimeSpan.FromSeconds(20);

        /// <summary>
        /// The delay before another attempt to check the status of the started operation.
        /// </summary>
        internal static TimeSpan OperationPeriod { get; set; } = TimeSpan.FromSeconds(130);

        /// <summary>
        /// The dalay before next reminder execution if the current one ended with an exception
        /// </summary>
        internal static TimeSpan ErrorDelay { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Enables delay randomness (disabling is usefull for testing).
        /// </summary>
        internal static bool UseDelayJitter { get; set; } = true;

        private readonly static Random _random = new Random();
        private readonly StateItem<ResourceScalingActorConfiguration> _configuration;
        private readonly StateItem<ResourceScalingActorState> _state;
        private readonly IResourceScalerFactory _resourceScalerFactory;

        public ResourceScalingActor(ActorService actorService, ActorId actorId, IBigBrother bigBrother, IResourceScalerFactory resourceScalerFactory)
            : base(actorService, actorId, bigBrother)
        {
            _configuration = new StateItem<ResourceScalingActorConfiguration>(StateManager, "configuration");
            _state = new StateItem<ResourceScalingActorState>(StateManager, "state");
            _resourceScalerFactory = resourceScalerFactory;
        }

        async Task IResourceScalingActor.Configure(ResourceScalingActorConfiguration configuration)
        {
            if (configuration == null)
                await Disable();
            else
                await Enable(configuration);

            BigBrother.Publish(new ResourceScalingActorConfigured
            {
                Enabled = configuration != null,
            });
        }

        private async Task Disable()
        {
            await _configuration.Set(null);
            await _state.Set(null);
        }

        public async Task Enable(ResourceScalingActorConfiguration configuration)
        {
            await _configuration.Set(configuration);
            await _state.TryAdd(new ResourceScalingActorState { RequestedThroughput = -1, OperationCompleted = true });
        }

        async Task<ScalingResult<bool>> IResourceScalingActor.ScaleIn()
        {
            var state = (await _state.TryGet()).Value;
            if (state == null)
            {
                BigBrother.Publish(new BullfrogException($"The actor {Id} is not enabled and the ScaleIn operation cannot be performed.").ToExceptionEvent());
                // Pretend that the operation completed to prevent repeating calls.
                return ScalingResult.FromValue(true);
            }

            if (state.RequestedThroughput == null)
            {
                return ScalingResult.FromValue(state.OperationCompleted, state.ExceptionMessaage);
            }

            state.RequestedThroughput = null;
            state.RequestedEndsAt = null;
            state.OperationCompleted = false;
            state.FinalThroughput = null;
            await _state.Set(state);

            await WakeMe(OperationStartDelay);

            return ScalingResult.FromValue(state.OperationCompleted);
        }

        async Task<ScalingResult<int?>> IResourceScalingActor.ScaleOut(int throughput, DateTimeOffset endsAt)
        {
            var state = (await _state.TryGet()).Value;
            if (state == null)
            {
                BigBrother.Publish(new BullfrogException($"The actor {Id} is not enabled and the ScaleOut operation cannot be performed.").ToExceptionEvent());
                // Pretend that the operation completed to prevent repeating calls.
                return ScalingResult.FromValue<int?>(0);
            }

            if (state.RequestedThroughput == throughput && state.RequestedEndsAt == endsAt)
            {
                return ScalingResult.FromValue(state.FinalThroughput, state.ExceptionMessaage);
            }

            state.RequestedThroughput = throughput;
            state.RequestedEndsAt = endsAt;
            state.OperationCompleted = false;
            state.FinalThroughput = null;
            await _state.Set(state);

            await WakeMe(OperationStartDelay);

            return ScalingResult.FromValue(state.FinalThroughput);
        }

        async Task IRemindable.ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
        {
            try
            {
                var actorState = await _state.Get();
                try
                {
                    if (actorState == null || actorState.OperationCompleted)
                        return;

                    actorState.ExceptionMessaage = null;
                    var resourceScaler = await CreateResourceScaler();
                    resourceScaler.SerializedState = actorState.ResourceScalerState;
                    if (actorState.RequestedThroughput.HasValue)
                    {
                        var result = await resourceScaler.ScaleOut(actorState.RequestedThroughput.Value, actorState.RequestedEndsAt.Value);
                        if (result.HasValue)
                        {
                            actorState.OperationCompleted = true;
                            actorState.FinalThroughput = result.Value;
                        }
                    }
                    else
                    {
                        actorState.OperationCompleted = await resourceScaler.ScaleIn();
                    }

                    actorState.ResourceScalerState = resourceScaler.SerializedState;
                    await _state.Set(actorState);

                    if (!actorState.OperationCompleted)
                        await WakeMe(OperationPeriod);
                }
                catch (Exception ex)
                {
                    actorState.ExceptionMessaage = ex.Message;
                    await _state.Set(actorState);
                    throw;
                }
            }
            catch
            {
                await WakeMe(ErrorDelay);
            }
        }

        private async Task<ResourceScaler> CreateResourceScaler()
        {
            var configuration = await _configuration.Get();
            var smc = new ScaleManagerConfiguration
            {
                CosmosConfigurations = new System.Collections.Generic.List<CosmosConfiguration>(),
                ScaleSetConfigurations = new System.Collections.Generic.List<ScaleSetConfiguration>(),
                AutomationAccounts = configuration.AutomationAccounts,
            };
            if (configuration.CosmosConfiguration != null)
            {
                smc.CosmosConfigurations.Add(configuration.CosmosConfiguration);
            }
            else
            {
                smc.ScaleSetConfigurations.Add(configuration.ScaleSetConfiguration);
            }

            return _resourceScalerFactory.CreateScaler(configuration.CosmosConfiguration?.Name ?? configuration.ScaleSetConfiguration.Name, smc);
        }

        private async Task WakeMe(TimeSpan after)
        {
            if (UseDelayJitter)
                after += RandomTimespanUpTo(TimeSpan.FromSeconds(10));

            await RegisterReminderAsync(
                "reminder",
                null,
                after,
                TimeSpan.FromMilliseconds(-1));
        }

        private static TimeSpan RandomTimespanUpTo(TimeSpan max)
        {
            lock (_random)
            {
                return TimeSpan.FromMilliseconds(_random.Next((int)max.TotalMilliseconds));
            }
        }
    }
}
