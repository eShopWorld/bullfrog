using System.IO;

namespace Bullfrog.Client
{
    public static class SwaggerDefinition
    {
        public static string Get()
        {
            using (var stream = typeof(SwaggerDefinition).Assembly.GetManifestResourceStream(typeof(SwaggerDefinition), "Client.json"))
            using (var textStream = new StreamReader(stream))
            {
                return textStream.ReadToEnd();
            }
        }
    }
}
