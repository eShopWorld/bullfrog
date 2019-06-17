using Bullfrog.Actors.Helpers;
using Bullfrog.Actors.Interfaces.Models;
using Eshopworld.Core;
using Microsoft.Azure.Management.Fluent;

namespace Bullfrog.Actors.ResourceScalers
{
    public class ScaleSetScalerFactory
    {
        private readonly Azure.IAuthenticated _authenticated;
        private readonly ScaleSetMonitor _scaleSetMonitor;
        private readonly IBigBrother _bigBrother;

        public ScaleSetScalerFactory(Azure.IAuthenticated authenticated, ScaleSetMonitor scaleSetMonitor, IBigBrother bigBrother)
        {
            _authenticated = authenticated;
            _scaleSetMonitor = scaleSetMonitor;
            _bigBrother = bigBrother;
        }

        public ResourceScaler CreateScaler(ScaleSetConfiguration configuration)
        {
            return new ScaleSetScaler(_authenticated, configuration, _scaleSetMonitor, _bigBrother);
        }
    }
}
