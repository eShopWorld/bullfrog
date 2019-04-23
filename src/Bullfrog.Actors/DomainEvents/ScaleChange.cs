using System;
using Eshopworld.Core;

namespace Bullfrog.DomainEvents
{
    public class ScaleChange : DomainEvent
    {
        public Guid Id { get; set; }

        public ScaleChangeType Type { get; set; }
    }
}
