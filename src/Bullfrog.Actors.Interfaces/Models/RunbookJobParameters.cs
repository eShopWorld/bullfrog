using System.Collections.Generic;

namespace Bullfrog.Actors.Interfaces.Models
{
    public class RunbookJobParameters
    {
        public string VmssName { get; set; }

        public Dictionary<string, object> Parameters { get; set; }
    }
}
