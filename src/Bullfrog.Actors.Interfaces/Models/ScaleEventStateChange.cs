using System;
using Bullfrog.DomainEvents;

namespace Bullfrog.Actors.Interfaces.Models
{
    public class ScaleEventStateChange
    {
        public Guid EventId { get; set; }

        public ScaleChangeType State { get; set; }
    }
}
