﻿using System.Threading;
using System.Threading.Tasks;

namespace Bullfrog.Actor.Interfaces
{
    interface ICosmoDbConfigurator
    {
        Task Configure(int x, CancellationToken cancellationToken);

        Task SetThroughput(int rus, CancellationToken cancellationToken);
    }
}
