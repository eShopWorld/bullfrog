using System;
using System.Threading.Tasks;
using Bullfrog.Common.Events;
using Eshopworld.Core;

namespace Bullfrog.Common.Telemetry
{
    public static class BigBrotherExtensions
    {
        public static async Task LogAzureCallDuration(this IBigBrother bigBrother, string operation, string resourceId, Func<Task> action)
        {
            var durationEvent = new AzureOperationDurationEvent
            {
                Operation = operation,
                ResourceId = resourceId,
            };
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                durationEvent.ExceptionMessage = ex.Message;
                bigBrother.Publish(durationEvent);

                throw;
            }

            bigBrother.Publish(durationEvent);
        }

        public static async Task<T> LogAzureCallDuration<T>(this IBigBrother bigBrother, string operation, string resourceId, Func<Task<T>> action)
        {
            var durationEvent = new AzureOperationDurationEvent
            {
                Operation = operation,
                ResourceId = resourceId,
            };
            T t;
            try
            {
                t = await action();
            }
            catch (Exception ex)
            {
                durationEvent.ExceptionMessage = ex.Message;
                bigBrother.Publish(durationEvent);

                throw;
            }

            bigBrother.Publish(durationEvent);

            return t;
        }
    }
}
