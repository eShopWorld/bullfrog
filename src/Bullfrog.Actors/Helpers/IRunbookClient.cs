using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bullfrog.Actors.Helpers
{
    /// <summary>
    /// Automation runbook client
    /// </summary>
    public interface IRunbookClient
    {
        /// <summary>
        /// Starts an automation job.
        /// </summary>
        /// <param name="parameters">The details of the job.</param>
        /// <returns>The identifier of the job.</returns>
        Task<Guid> CreateJob(RunbookJobCreationParameters parameters);

        /// <summary>
        /// Checks the status of the automation job.
        /// </summary>
        /// <param name="resourceId">The automation account's resource id.</param>
        /// <param name="jobId">The job id.</param>
        /// <returns>The status of the job.</returns>
        Task<RunbookJobExecutionStatus> GetJobStatus(string resourceId, Guid jobId);
    }

    /// <summary>
    /// Job creation parameters.
    /// </summary>
    public class RunbookJobCreationParameters
    {
        /// <summary>
        /// The automation account's resource id.
        /// </summary>
        public string AutomationAccountResourceId { get; set; }

        /// <summary>
        /// The runbook name.
        /// </summary>
        public string RunbookName { get; set; }

        /// <summary>
        /// The runbook's parameters.
        /// </summary>
        public Dictionary<string, object> RunbookParameters { get; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// The automation job's execution status.
    /// </summary>
    public class RunbookJobExecutionStatus
    {
        /// <summary>
        /// The status of the job.
        /// </summary>
        public RunbookJobStatus Status { get; set; }

        /// <summary>
        /// The provisioning state of the job.
        /// </summary>
        public string ProvisioningState { get; set; }

        /// <summary>
        /// The status of the job reported by the automation job API
        /// </summary>
        public string ReportedStatus { get; set; }

        /// <summary>
        /// The optional exception thrown by the runbook.
        /// </summary>
        public string  Exception { get; set; }
    }

    /// <summary>
    /// The job's execution status.
    /// </summary>
    public enum RunbookJobStatus
    {
        /// <summary>
        /// The job waiting for execution or is currently executed.
        /// </summary>
        Processing,

        /// <summary>
        /// The job failed to executed successfully.
        /// </summary>
        Failed,

        /// <summary>
        /// The job completed its execution sucessfully.
        /// </summary>
        Succeeded,
    }
}
