using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bullfrog.Actors.EventModels;
using Bullfrog.Actors.Interfaces;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.Actors.Models;
using Bullfrog.Common;
using Bullfrog.DomainEvents;
using Eshopworld.Core;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;

namespace Bullfrog.Actors
{
    /// <summary>
    /// Reports changes of status of scale events by sending domain events.
    /// </summary>
    [StatePersistence(StatePersistence.Persisted)]
    public class ScaleEventStateReporter : BullfrogActorBase, IScaleEventStateReporter
    {
        private readonly string _scaleGroupName;
        private readonly StateItem<Dictionary<Guid, ScaleEventCurrentState>> _events;
        private readonly StateItem<HashSet<string>> _regions;

        public ScaleEventStateReporter(
            ActorService actorService,
            ActorId actorId,
            IBigBrother bigBrother)
            : base(actorService, actorId, bigBrother)
        {
            _events = new StateItem<Dictionary<Guid, ScaleEventCurrentState>>(StateManager, "events");
            _regions = new StateItem<HashSet<string>>(StateManager, "regions");
            var match = Regex.Match(actorId.ToString(), ":([^/]+)$");
            if (!match.Success)
                throw new BullfrogException($"The ActorId {actorId} has invalid format.");
            _scaleGroupName = match.Groups[1].Value;
        }

        async Task IScaleEventStateReporter.ConfigureRegions(string[] regions)
        {
            if (regions != null)
            {
                await _regions.Set(new HashSet<string>(regions));
                await _events.TryAdd(new Dictionary<Guid, ScaleEventCurrentState>());
            }
            else
            {
                await _regions.Set(null);
                await _events.TryRemove();
            }
        }

        async Task IScaleEventStateReporter.ReportScaleEventState(string region, List<ScaleEventStateChange> changes)
        {
            var scaleEvents = await _events.Get();
            HashSet<string> regions = null;
            foreach (var change in changes)
            {
                if (!scaleEvents.TryGetValue(change.EventId, out var scaleEvent))
                {
                    if (regions == null)
                        regions = await _regions.Get();
                    scaleEvent = new ScaleEventCurrentState
                    {
                        Regions = regions.ToDictionary(x => x, x => ScaleChangeType.Waiting),
                        ReportedState = ScaleChangeType.Waiting,
                    };
                    scaleEvents.Add(change.EventId, scaleEvent);
                }

                scaleEvent.Regions[region] = change.State;
                ReportEventStateChange(_scaleGroupName, change.EventId, scaleEvent);
                if (scaleEvent.ReportedState == ScaleChangeType.ScaleInComplete)
                    scaleEvents.Remove(change.EventId);
            }

            await _events.Set(scaleEvents);
        }

        async Task IScaleEventStateReporter.PurgeScaleEvents(List<Guid> scaleEvents)
        {
            var events = await _events.Get();
            var changed = false;
            foreach (var id in scaleEvents)
            {
                if (events.TryGetValue(id, out var sc))
                {
                    BigBrother.Publish(new PurgingNotCompletedEvent
                    {
                        ScaleEventId = id,
                        State = sc.CurrentState,
                        RegionsSummary = string.Join("; ", sc.Regions.Select(kv => $"{kv.Key}={kv.Value}")),
                    });
                    events.Remove(id);
                    changed = true;
                }
            }

            if (changed)
                await _events.Set(events);
        }

        private void ReportEventStateChange(string scaleGroup, Guid eventId, ScaleEventCurrentState scaleEvent)
        {
            var currentState = scaleEvent.CurrentState;
            if (currentState == ScaleChangeType.Waiting)
                return;
            if (currentState == scaleEvent.ReportedState)
                return;

            BigBrother.Publish(new ScaleChange
            {
                Id = eventId,
                Type = currentState,
                ScaleGroup = scaleGroup,
            });

            scaleEvent.ReportedState = currentState;
        }
    }
}
