﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Bullfrog.Actors.Models
{
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