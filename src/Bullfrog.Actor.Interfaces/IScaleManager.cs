using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bullfrog.Actor.Interfaces.Models;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Services.Remoting;

[assembly: FabricTransportActorRemotingProvider(RemotingListenerVersion = RemotingListenerVersion.V2, RemotingClientVersion = RemotingClientVersion.V2)]
namespace Bullfrog.Actor.Interfaces
{
    public interface IScaleManager : IActor
    {
        Task<UpdatedScheduledScaleEvent> ScheduleScaleEvent(ScaleEvent scaleEvent, CancellationToken cancellationToken);

        Task<ScheduledScaleEvent> GetScaleEvent(Guid id, CancellationToken cancellationToken);

        Task<List<ScheduledScaleEvent>> ListScaleEvents(CancellationToken cancellationToken);

        Task<ScaleEventState> DeleteScaleEvent(Guid id, CancellationToken cancellationToken);

        Task<ScaleState> GetScaleSet(CancellationToken cancellationToken);
    }
}
