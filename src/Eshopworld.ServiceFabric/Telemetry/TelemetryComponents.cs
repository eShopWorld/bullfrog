using System;
using System.Collections.Generic;
using System.Fabric;
using System.Runtime.Serialization;
using Microsoft.ApplicationInsights.ServiceFabric;
using Microsoft.ServiceFabric.Services.Remoting.V2.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2;
using System.Threading.Tasks;

namespace Eshopworld.ServiceFabric.Telemetry
{
    /// <summary>
    /// Proxy dispatcher which can initialize request telemetry
    /// </summary>
    internal class TelemetryContextInitializingDispatcher : IServiceRemotingMessageHandler
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

    internal class DataContractRemotingMessageFactory : IServiceRemotingMessageBodyFactory
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
    internal class MyServiceRemotingRequestMessageBody : IServiceRemotingRequestMessageBody
    {
        [DataMember(Name = "parameters")]
        private readonly Dictionary<string, object> _parameters;

        public MyServiceRemotingRequestMessageBody(int parameterInfos)
        {
            _parameters = new Dictionary<string, object>(parameterInfos);
        }

        public void SetParameter(int position, string parameName, object parameter)
{
            _parameters[parameName] = parameter;
        }

        public object GetParameter(int position, string parameName, Type paramType)
        {
            return _parameters[parameName];
        }
    }

    [DataContract(Name = "msgResponse", Namespace = Constants.ServiceCommunicationNamespace)]
    internal class MyServiceRemotingResponseMessageBody : IServiceRemotingResponseMessageBody
    {
        [DataMember(Name = "response")]
        private object _response;

        public void Set(object response)
        {
            _response = response;
        }

        public object Get(Type paramType)
        {
            return _response;
        }
    }

    internal static class Constants
    {
        public const string ServiceCommunicationNamespace = "urn:ServiceFabric.Communication";
        public const string ListenerNameV2 = "V2Listener";
    }
}
