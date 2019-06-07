using Bullfrog.Actors.Helpers;
using Bullfrog.Actors.Interfaces.Models;
using Eshopworld.Telemetry;
using Microsoft.Azure.Management.Fluent;

namespace Bullfrog.Actors.Modules
{
    internal class ScaleSetScalingModuleFactory : IScaleSetScalingModuleFactory
    {
        private readonly Azure.IAuthenticated _authenticated;
        private readonly IScaleSetMonitor _scaleSetMonitor;
        private readonly BigBrother _bigBrother;

        public ScaleSetScalingModuleFactory(Azure.IAuthenticated authenticated, IScaleSetMonitor scaleSetMonitor, BigBrother bigBrother)
        {
            _authenticated = authenticated;
            _scaleSetMonitor = scaleSetMonitor;
            _bigBrother = bigBrother;
        }

        public ScalingModule CreateModule(ScaleSetConfiguration configuration)
        {
            return new ScaleSetModule(_authenticated, configuration, _scaleSetMonitor, _bigBrother);
        }
    }
}
