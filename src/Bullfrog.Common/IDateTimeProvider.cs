using System;

namespace Bullfrog.Common
{
    public interface IDateTimeProvider
    {
        DateTimeOffset UtcNow { get; }
    }
}
