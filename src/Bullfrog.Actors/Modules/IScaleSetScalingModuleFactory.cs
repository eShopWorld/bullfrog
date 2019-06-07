using Bullfrog.Actors.Interfaces.Models;

namespace Bullfrog.Actors.Modules
{
    /// <summary>
    /// Factory of scale set scaling modules.
    /// </summary>
    public interface IScaleSetScalingModuleFactory
    {
        /// <summary>
        /// Creates a scaling module for the specified scale set configuration.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <returns></returns>
        ScalingModule CreateModule(ScaleSetConfiguration configuration);
    }
}
