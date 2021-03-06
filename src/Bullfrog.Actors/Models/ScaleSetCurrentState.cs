﻿using System.Diagnostics.CodeAnalysis;

namespace Bullfrog.Actors.Models
{
    [ExcludeFromCodeCoverage]
    public class ScaleSetCurrentState
    {
        /// <summary>
        /// The number of all instances in the scale set.
        /// </summary>
        public int AllInstancesNumber { get; set; }

        /// <summary>
        /// The number of instances reported by a load balancer as working
        /// </summary>
        public int OperationalInstancesNumber { get; set; }
    }
}
