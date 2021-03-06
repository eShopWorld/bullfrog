﻿using Eshopworld.Telemetry;
using Microsoft.ServiceFabric.Services.Runtime;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Bullfrog.Api
{
    [ExcludeFromCodeCoverage]
    internal static class Program
    {
        /// <summary>
        /// This is the entry point of the service host process.
        /// </summary>
        private static void Main()
        {
            try
            {
                // The ServiceManifest.XML file defines one or more service type names.
                // Registering a service maps a service type name to a .NET type.
                // When Service Fabric creates an instance of this service type,
                // an instance of the class is created in this host process.

                ServiceRuntime.RegisterServiceAsync("Bullfrog.ApiType",
                    context => new ApiService(context)).GetAwaiter().GetResult();

                // Prevents this host process from terminating so services keeps running. 
                Thread.Sleep(Timeout.Infinite);
            }
            catch (Exception ex)
            {
                BigBrother.Write(ex);
                ServiceEventSource.Current.ServiceHostInitializationFailed(ex.ToString());
                throw;
            }
        }
    }
}
