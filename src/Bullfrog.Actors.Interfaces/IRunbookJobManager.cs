using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces.Models;
using Microsoft.ServiceFabric.Actors;

namespace Bullfrog.Actors.Interfaces
{
    /// <summary>
    /// Represents an actor responsible for creating runbook jobs.
    /// </summary>
    public interface IRunbookJobManager : IActor
    {
        /// <summary>
        /// Configures the actor.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <returns></returns>
        Task Configure(RunbookJobManagerConfiguration configuration);

        /// <summary>
        /// Starts a job
        /// </summary>
        /// <param name="parameters">The job parameters.</param>
        /// <returns></returns>
        Task StartJob(RunbookJobParameters parameters);

        //Task<bool> JobCompleted(string name);
    }
}
