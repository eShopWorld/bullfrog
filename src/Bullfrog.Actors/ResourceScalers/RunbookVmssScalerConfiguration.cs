using Bullfrog.Actors.Interfaces.Models;

namespace Bullfrog.Actors.ResourceScalers
{
    public class RunbookVmssScalerConfiguration
    {
        public ScaleSetConfiguration ScaleSet { get; set; }

        public string ScaleGroup { get; set; }

        public string Region { get; set; }

        public string AutomationAccountResourceId { get; set; }
    }
}
