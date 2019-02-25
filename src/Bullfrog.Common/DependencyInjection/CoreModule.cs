using System;
using System.Collections.Generic;
using System.Text;
using Autofac;
using Eshopworld.Core;
using Eshopworld.DevOps;
using Eshopworld.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.ServiceFabric.Actors.Client;

namespace Bullfrog.Common.DependencyInjection
{
    /// <summary>
    /// some key  - devops + runtime -  level services are registered here
    /// </summary>
    public class CoreModule : Module
    {
        private bool TestMode { get; }

        /// <summary>
        /// core module constructor allowing to enable test mode - disabled by default
        /// </summary>
        /// <param name="testMode">test mode flag</param>
        public CoreModule(bool testMode = false)
        {
            TestMode = testMode;
        }

        protected override void Load(ContainerBuilder builder)
        {
            var config = EswDevOpsSdk.BuildConfiguration(TestMode);

            builder.RegisterInstance(config)
                   .As<IConfigurationRoot>()
                   .SingleInstance();

            builder.Register<IBigBrother>(c =>
            {
                var insKey = c.Resolve<IConfigurationRoot>()["BBInstrumentationKey"];
                return new BigBrother(insKey, insKey);
            })
            .SingleInstance();

            builder.RegisterType<ActorProxyFactory>().As<IActorProxyFactory>();
        }
    }
}
