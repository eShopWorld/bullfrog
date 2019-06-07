using Bullfrog.Actors.Interfaces.Models;

namespace Bullfrog.Actors.Modules
{
    public interface IScaleModuleFactory
    {
        ScalingModule CreateModule(string name, ScaleManagerConfiguration configuration);
    }
}
