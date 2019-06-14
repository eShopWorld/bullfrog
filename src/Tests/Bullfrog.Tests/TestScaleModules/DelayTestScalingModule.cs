using System;
using System.Threading.Tasks;
using Bullfrog.Actors.Modules;

namespace TestScaleModules
{
    internal class DelayTestScalingModule : ScalingModule
    {
        private readonly TimeSpan _delay;
        private readonly int _minThroughput;
        private readonly Func<DateTimeOffset> _now;
        private int _currentThroughput;
        private DateTimeOffset? _scalingTill;

        public DelayTestScalingModule(TimeSpan delay, int minThroughput, Func<DateTimeOffset> now)
        {
            _delay = delay;
            _minThroughput = minThroughput;
            _now = now;
            _currentThroughput = minThroughput;
        }

        public override Task<int?> SetThroughput(int? newThroughput)
        {
            if (_currentThroughput != (newThroughput ?? _minThroughput))
            {
                _scalingTill = _now().Add(_delay);
                _currentThroughput = newThroughput ?? _minThroughput;
            }

            return Task.FromResult(_now() < _scalingTill ? (int?)null : _currentThroughput);
        }
    }
}
