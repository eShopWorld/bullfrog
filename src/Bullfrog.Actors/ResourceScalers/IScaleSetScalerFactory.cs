using Bullfrog.Actors.Interfaces.Models;

namespace Bullfrog.Actors.ResourceScalers
{
    /// <summary>
    /// Factory of scale set scalers.
    /// </summary>
    public interface IScaleSetScalerFactory
    {
        /// <summary>
        /// Creates a scaler for the specified scale set configuration.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <returns></returns>
        ResourceScaler CreateScaler(ScaleSetConfiguration configuration);
    }
}
