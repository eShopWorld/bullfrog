using Bullfrog.Actors.Interfaces.Models;

namespace Bullfrog.Actors.ResourceScalers
{
    public interface IResourceScalerFactory
    {
        ResourceScaler CreateScaler(string name, ScaleManagerConfiguration configuration);
    }
}
