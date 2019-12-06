using System;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces.Models;

namespace Bullfrog.Actors.ResourceScalers
{
    public class RunbookVmssScaler : ResourceScaler
    {
        private readonly ScaleSetConfiguration _configuration;
        private readonly string _scaleGroup;
        private readonly string _region;

        public RunbookVmssScaler(RunbookVmssScalerConfiguration configuration)
        {
            _configuration = configuration.ScaleSet;
            _scaleGroup = configuration.ScaleGroup;
            _region = configuration.Region;
        }

        public override async Task<bool> ScaleIn()
        {
            return true;
        }

        public override async Task<int?> ScaleOut(int throughput, DateTimeOffset endsAt)
        {
            return 0;
        }
    }
}
