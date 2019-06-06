using System;

namespace Bullfrog.Actors.Modules
{
    public class ScaleChangeFailedEventArgs : EventArgs
    {
        public Exception Exception { get; }

        public ScaleChangeFailedEventArgs(Exception exception)
        {
            Exception = exception;
        }
    }
}
