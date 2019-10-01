using Eshopworld.Telemetry;

namespace Eshopworld.ServiceFabric.DependencyInjection
{
    /// <summary>
    /// Configures a BigBrother instance during its initialization.
    /// </summary>
    public interface IBigBrotherConfigurator
    {
        /// <summary>
        /// Performs the configuration of the BigBrother instance.
        /// </summary>
        /// <param name="bigBrother">The BigBrother instance.</param>
        void Initialize(BigBrother bigBrother);
    }
}
