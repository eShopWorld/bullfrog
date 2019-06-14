using Bullfrog.Actors.Helpers;
using Bullfrog.Actors.Interfaces.Models;
using Eshopworld.Core;
using Microsoft.Azure.Management.Fluent;

namespace Bullfrog.Actors.Modules
{
    internal class ScaleSetScalingModuleFactory : IScaleSetScalingModuleFactory
    {
        private readonly Azure.IAuthenticated _authenticated;
        private readonly IScaleSetMonitor _scaleSetMonitor;
        private readonly IBigBrother _bigBrother;

        public ScaleSetScalingModuleFactory(Azure.IAuthenticated authenticated, IScaleSetMonitor scaleSetMonitor, IBigBrother bigBrother)
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
