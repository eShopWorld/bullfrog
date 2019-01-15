using System;
using System.Collections.Generic;
using System.Fabric.Description;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Eshopworld.Core;
using Eshopworld.DevOps;
using Eshopworld.Telemetry;
using Microsoft.AspNetCore.Hosting;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;

namespace Bullfrog.Api
{
    public static class EswSslExtentions
    {
        public static IWebHostBuilder UseEswSsl(this IWebHostBuilder builder, int port, bool isHttps)
        {
            return builder.UseEswSsl(new[] { (port, isHttps) });
        }

        public static IWebHostBuilder UseEswSsl(this IWebHostBuilder builder, AspNetCoreCommunicationListener listener)
        {
            var ports = from endpoint in listener.ServiceContext.CodePackageActivationContext.GetEndpoints()
                        where endpoint.EndpointType == EndpointType.Input
                        where endpoint.Protocol == EndpointProtocol.Http || endpoint.Protocol == EndpointProtocol.Https
                        select (endpoint.Port, endpoint.Protocol == EndpointProtocol.Https);
            return builder.UseEswSsl(ports);
        }

        public static IWebHostBuilder UseEswSsl(this IWebHostBuilder builder, IEnumerable<(int port, bool isHttps)> endpoints)
        {
            return builder.ConfigureKestrel((context, options) =>
            {
                X509Certificate2 cert = null;
                foreach (var (port, isHttps) in endpoints)
                {
                    options.Listen(IPAddress.Any, port, listenOptions =>
                    {
                        if (isHttps)
                        {
                            try
                            {
                                if (cert == null)
                                    cert = GetCertificate(context.HostingEnvironment.EnvironmentName);
                                listenOptions.UseHttps(cert);
                                listenOptions.NoDelay = true;
                            }
                            catch (Exception ex)
                            {
                                BigBrother.Write(ex.ToExceptionEvent());
                                // TODO: can we do anything else than reverting to HTTP?
                            }
                        }
                    });
                }
            });
        }

        private static X509Certificate2 GetCertificate(string environmentName)
        {
            if (!Enum.TryParse<DeploymentEnvironment>(environmentName, true, out var environment))
            {
                environment = DeploymentEnvironment.Development;   // TODO: other default or exception? BB?
            }
            return GetCertificate(environment);
        }

        private static X509Certificate2 GetCertificate(DeploymentEnvironment environment)
        {
            var subject = GetCertSubjectName(environment);
            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            try
            {
                store.Open(OpenFlags.ReadOnly);
                var certCollection = store.Certificates.Find(X509FindType.FindBySubjectDistinguishedName, $"CN={subject}, OU=Domain Control Validated", true);

                if (certCollection.Count == 0)
                {
                    // TODO: another temporary attempt to find the cert
                    certCollection = store.Certificates.Find(X509FindType.FindBySubjectName, subject, false);

                    for (int i = 0; i < certCollection.Count; i++)
                    {
                        if(!IsSslCertificate(certCollection[i]))
                        {
                            certCollection.RemoveAt(i);
                            i--;
                        }
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

        /// <summary>
        /// Checks whether the certificate can be used to enable SSL trafic to the local server
        /// </summary>
        /// <param name="cert">Certificate to check.</param>
        /// <returns>True if the certificate can be used for SSL decoding, false otherwise.</returns>
        private static bool IsSslCertificate(X509Certificate2 cert)
        {
            // based on https://stackoverflow.com/questions/51322480/x509-certificate-intended-purpose-and-ssl
            if (!cert.HasPrivateKey)
            {
                return false;
            }

            foreach (var extension in cert.Extensions)
            {
                if (extension.Oid.Value == "2.5.29.37")  // Enhanced Key Usage
                {
                    var eku = (X509EnhancedKeyUsageExtension)extension;
                    foreach (var oid in eku.EnhancedKeyUsages)
                    {
                        if (oid.Value == "1.3.6.1.5.5.7.3.1") // Server Authentication
                            return true;
                    }
                }
            }

            return false;
        }
    }
}
