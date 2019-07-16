﻿namespace Bullfrog.Actors.EventModels
{
    /// <summary>
    /// Reports the execution time of actor's methods.
    /// </summary>
    public class ActorMethodDuration : Eshopworld.Core.TimedTelemetryEvent
    {
        /// <summary>
        /// The name of the method.
        /// </summary>
        public string Name { get; set; }
    }
}
