using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
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
                     var environment = context.HostingEnvironment.EnvironmentName;
                     var cert = GetCertificate(environment);
                     listenOptions.UseHttps(cert);
                     listenOptions.NoDelay = true;
                 });
             });
        }

        private static X509Certificate2 GetCertificate(string environment)
        {
            var tld = "PROD".Equals(environment, StringComparison.OrdinalIgnoreCase) ? "com" : "net";
            var subject = $"star.{environment}.eshopworld.{tld}";
            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            try
            {
                store.Open(OpenFlags.ReadOnly);
                var certCollection = store.Certificates.Find(X509FindType.FindBySubjectDistinguishedName,$"CN={subject}", false);
                if (certCollection.Count == 0)
                {
                    if("DEVELOPMENT".Equals(environment, StringComparison.OrdinalIgnoreCase))
                    {
                        var devCerts = store.Certificates.Find(X509FindType.FindBySubjectDistinguishedName, "CN=localhost", false);
                        if (devCerts.Count > 0)
                        {
                            return devCerts[0];
                        }
                    }
                    throw new Exception($"The certificate {subject} has not been found.");
                }
                return certCollection[0];
            }
            finally
            {
                store.Close();
            }
        }
    }
}
