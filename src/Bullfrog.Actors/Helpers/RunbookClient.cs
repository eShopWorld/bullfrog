using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bullfrog.Common;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Newtonsoft.Json;

namespace Bullfrog.Actors.Helpers
{
    public class RunbookClient : IRunbookClient
    {
        private readonly RestClient _restClient;

        public RunbookClient(RestClient restClient)
        {
            _restClient = restClient;
        }

        public async Task<Guid> CreateJob(RunbookJobCreationParameters parameters)
        {
            var jobId = Guid.NewGuid();

            var body = new
            {
                properties = new
                {
                    runbook = new
                    {
                        name = parameters.RunbookName,// "VmssScaleTest",
                    },
                    parameters = parameters.RunbookParameters,
                },
            };
            var request = new HttpRequestMessage(HttpMethod.Put, GetJobApiUrl(parameters.AutomationAccountResourceId, jobId));
            var jsonBody = JsonConvert.SerializeObject(body);
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            await Execute(request);

            return jobId;
        }

        public async Task<RunbookJobExecutionStatus> GetJobStatus(string resourceId, Guid jobId)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, GetJobApiUrl(resourceId, jobId));
            var response = await Execute(request);

            RunbookJobStatus status;
            switch (response.Properties.ProvisioningState)
            {
                case "Processing":
                    status = RunbookJobStatus.Processing;
                    break;
                case "Succeeded":
                    status = RunbookJobStatus.Succeeded;
                    break;
                default:
                    status = RunbookJobStatus.Failed;
                    break;
            }

            return new RunbookJobExecutionStatus
            {
                Exception = response.Properties.Exception,
                ProvisioningState = response.Properties.ProvisioningState,
                ReportedStatus = response.Properties.Status,
                Status = status,
            }; 
        }

        private string GetJobApiUrl(string resourceId, Guid jobId)
        {
            return "https://management.azure.com" + resourceId + $"/jobs/{jobId}?api-version=2015-10-31";
        }

        private async Task<JobStatus> Execute(HttpRequestMessage requestMessage, CancellationToken cancellationToken = default)
        {
            var subscriptionClient = new SubscriptionClient(_restClient);

            if (subscriptionClient.Credentials != null)
            {
                await subscriptionClient.Credentials.ProcessHttpRequestAsync(requestMessage, cancellationToken);
            }

            var response = await subscriptionClient.HttpClient.SendAsync(requestMessage, cancellationToken);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new BullfrogException($"The call to {requestMessage.RequestUri} failed with the status code {response.StatusCode} {response.ReasonPhrase}. The body of the response is: {content}");
            }

            return JsonConvert.DeserializeObject<JobStatus>(content);
        }

        class JobStatus
        {
            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }

            [JsonProperty(PropertyName = "properties")]
            public JobStatusProperties Properties { get; set; }
        }

        class JobStatusProperties
        {
            [JsonProperty(PropertyName = "jobId")]
            public Guid JobId { get; set; }

            [JsonProperty(PropertyName = "provisioningState")]
            public string ProvisioningState { get; set; }

            [JsonProperty(PropertyName = "creationTime")]
            public DateTimeOffset CreationTime { get; set; }

            [JsonProperty(PropertyName = "endTime")]
            public DateTimeOffset? EndTime { get; set; }

            [JsonProperty(PropertyName = "exception")]
            public string Exception { get; set; }

            [JsonProperty(PropertyName = "lastModifiedTime")]
            public DateTimeOffset LastModifiedTime { get; set; }

            [JsonProperty(PropertyName = "lastStatusModifiedTime")]
            public DateTimeOffset LastStatusModifiedTime { get; set; }

            [JsonProperty(PropertyName = "startedBy")]
            public string StartedBy { get; set; }

            [JsonProperty(PropertyName = "startTime")]
            public DateTimeOffset? StartTime { get; set; }

            [JsonProperty(PropertyName = "status")]
            public string Status { get; set; }

            [JsonProperty(PropertyName = "statusDetails")]
            public string StatusDetails { get; set; }

            [JsonProperty(PropertyName = "parameters")]
            public Dictionary<string, string> Parameters { get; set; }
        }
    }
}

