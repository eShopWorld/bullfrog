using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Bullfrog.Actors.Modules
{
    class ScaleSetScalingModule : ScalingModule
    {
        public override Task ReceiveReminderAsync()
        {
            throw new NotImplementedException();
        }

        public override Task<int?> ResetThroughput()
        {
            throw new NotImplementedException();
        }

        public override Task<int?> SetThroughput(int throughput)
        {
            throw new NotImplementedException();
        }
    }
}
