using System;
using System.Collections.Generic;
using System.Fabric;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights.ServiceFabric;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Actors.Remoting.V2.Runtime;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2;
using Microsoft.ServiceFabric.Services.Remoting.V2.Runtime;

namespace Bullfrog.Actors
{
    /// <summary>
    /// Creates actors with telemetry components injected into remoting pipeline to track requests and dependecies.
    /// </summary>
    internal class MonitoredActorService : ActorService
    {
        public MonitoredActorService(
             StatefulServiceContext context,
             ActorTypeInformation actorTypeInfo,
             Func<ActorService, ActorId, ActorBase> actorFactory = null,
             Func<ActorBase, IActorStateProvider, IActorStateManager> stateManagerFactory = null,
             IActorStateProvider stateProvider = null,
             ActorServiceSettings settings = null)
         : base(context, actorTypeInfo, actorFactory, stateManagerFactory, stateProvider, settings)
        {
        }


        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            var listeners = new List<ServiceReplicaListener>
            {
                new ServiceReplicaListener(ctx =>
               {
                   // Create a standard remoting dispatcher with a proxy which initializes the request telemetry
                   var dispatcher = new TelemetryContextInitializingDispatcher(new ActorServiceRemotingDispatcher(this, new DataContractRemotingMessageFactory()), ctx);
                   return new FabricTransportActorServiceRemotingListener(this, dispatcher);
               }, Constants.ListenerNameV2)
            };

            return listeners;
        }

        /// <summary>
        /// Proxy dispatcher which can initialize request telemetry
        /// </summary>
        private class TelemetryContextInitializingDispatcher : IServiceRemotingMessageHandler
        {
            private readonly IServiceRemotingMessageHandler _handler;
            private readonly ServiceContext _context;

            public TelemetryContextInitializingDispatcher(IServiceRemotingMessageHandler handler, ServiceContext context)
            {
                _handler = handler;
                _context = context;
            }

            public IServiceRemotingMessageBodyFactory GetRemotingMessageBodyFactory() => _handler.GetRemotingMessageBodyFactory();

            public void HandleOneWayMessage(IServiceRemotingRequestMessage requestMessage)
            {
                FabricTelemetryInitializerExtension.SetServiceCallContext(_context);
                _handler.HandleOneWayMessage(requestMessage);
            }

            public Task<IServiceRemotingResponseMessage> HandleRequestResponseAsync(IServiceRemotingRequestContext requestContext, IServiceRemotingRequestMessage requestMessage)
            {
                FabricTelemetryInitializerExtension.SetServiceCallContext(_context);
                return _handler.HandleRequestResponseAsync(requestContext, requestMessage);
            }
        }

        private class DataContractRemotingMessageFactory : IServiceRemotingMessageBodyFactory
        {
            public IServiceRemotingRequestMessageBody CreateRequest(string interfaceName, string methodName, int numberOfParameters, object wrappedRequestObject)
            {
                return new MyServiceRemotingRequestMessageBody(numberOfParameters);
            }

            public IServiceRemotingResponseMessageBody CreateResponse(string interfaceName, string methodName, object wrappedResponseObject)
            {
                return new MyServiceRemotingResponseMessageBody();
            }
        }

        [DataContract(Name = "msgBody", Namespace = Constants.ServiceCommunicationNamespace)]
        private class MyServiceRemotingRequestMessageBody : IServiceRemotingRequestMessageBody
        {
            [DataMember]
            private Dictionary<string, object> parameters;

            public MyServiceRemotingRequestMessageBody(int parameterInfos)
            {
                parameters = new Dictionary<string, object>(parameterInfos);
            }

            public void SetParameter(int position, string parameName, object parameter)
            {
                parameters[parameName] = parameter;
            }

            public object GetParameter(int position, string parameName, Type paramType)
            {
                return parameters[parameName];
            }
        }

        [DataContract(Name = "msgResponse", Namespace = Constants.ServiceCommunicationNamespace)]
        private class MyServiceRemotingResponseMessageBody : IServiceRemotingResponseMessageBody
        {
            [DataMember]
            private object response;

            public void Set(object response)
            {
                this.response = response;
            }

            public object Get(Type paramType)
            {
                return response;
            }
        }

        private static class Constants
        {
            public const string ServiceCommunicationNamespace = "urn:ServiceFabric.Communication";
            public const string ListenerNameV2 = "V2Listener";
        }
    }
}
