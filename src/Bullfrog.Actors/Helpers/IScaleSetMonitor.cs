using System.Threading.Tasks;

namespace Bullfrog.Actors.Helpers
{
    public interface IScaleSetMonitor
    {
        Task<int> GetNumberOfWorkingInstances(string loadBalancerResourceId, int healthProbePort); 
    }
}
