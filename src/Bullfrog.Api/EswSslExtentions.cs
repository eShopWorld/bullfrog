using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Eshopworld.Core;
using Eshopworld.DevOps;
using Eshopworld.Telemetry;
using Microsoft.AspNetCore.Hosting;

namespace Bullfrog.Api
{
    public static class EswSslExtentions
    {
        public static IWebHostBuilder UseEswSsl(this IWebHostBuilder builder, int port)
        {
            return builder.ConfigureKestrel((context, options) =>
             {
                 options.Listen(IPAddress.Any, port, listenOptions =>
                 {
                     try
                     {
                         var environmentName = context.HostingEnvironment.EnvironmentName;
                         if (!Enum.TryParse<DeploymentEnvironment>(environmentName, true, out var environment))
                         {
                             environment = DeploymentEnvironment.Development;   // TODO: other default or exception? BB?
                         }
                         var cert = GetCertificate(environment);
                         listenOptions.UseHttps(cert);
                         listenOptions.NoDelay = true;
                     }
                     catch (Exception ex)
                     {
                         BigBrother.Write(ex.ToExceptionEvent());
                         // TODO: can we do anything else than reverting to HTTP?
                     }
                 });
             });
        }

        private static X509Certificate2 GetCertificate(DeploymentEnvironment environment)
        {
            var subject = GetCertSubjectName(environment);
            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            try
            {
                store.Open(OpenFlags.ReadOnly);
                var certCollection = store.Certificates.Find(X509FindType.FindBySubjectDistinguishedName, $"CN={subject}", false);

                if (certCollection.Count == 0)
                {
                    // TODO: temporary attempt to find the cert
                    certCollection = store.Certificates.Find(X509FindType.FindBySubjectName, subject, false);
                    if (certCollection.Count > 0)
                    {
                        BigBrother.Write(new ExceptionEvent(new Exception($"Found {subject} using the FindBySubjectName option (cert {certCollection[0].Subject}).")));
                    }
                }

                if (certCollection.Count == 0)
                {
                    throw new Exception($"The certificate for {subject} has not been found.");
                }
                return certCollection[0];
            }
            finally
            {
                store.Close();
            }
        }

        private static string GetCertSubjectName(DeploymentEnvironment environment)
        {
            switch (environment)
            {
                case DeploymentEnvironment.CI:
                    return "*.ci.eshopworld.net";
                case DeploymentEnvironment.Sand:
                    return "*.sandbox.eshopworld.com";
                case DeploymentEnvironment.Test:
                    return "*.test.eshopworld.net";
                case DeploymentEnvironment.Prep:
                    return "*.preprod.eshopworld.net";
                case DeploymentEnvironment.Prod:
                    return "*.production.eshopworld.com";
                case DeploymentEnvironment.Development:
                    return "localhost";
                default:
                    throw new ArgumentOutOfRangeException(nameof(environment), environment, $"The environment {environment} is not recognized");
            }
        }
    }
}
