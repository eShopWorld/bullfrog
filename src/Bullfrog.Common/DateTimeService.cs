using System;
using System.Collections.Generic;
using System.Text;

namespace Bullfrog.Common
{
    public static class DateTimeService
    {
        public static Func<DateTimeOffset> DateTimeProvider { get; set; } = () => DateTimeOffset.UtcNow;

        public static DateTimeOffset UtcNow => DateTimeProvider();
    }
}
