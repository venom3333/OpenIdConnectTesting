using System;
using System.Net.Http;
using System.Threading.Tasks;
using Client.Handlers;
using Microsoft.Extensions.DependencyInjection;
using IdentityModel.Client;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;

namespace Client
{
    class Program
    {
        private static async Task Main(string[] args)
        {
            var insecureHandler = InsecureHttpsHandler.GetInsecureHandler();
            var builder = new HostBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHttpClient("DebugClient")
                        .ConfigureHttpMessageHandlerBuilder(handlerBuilder => 
                            handlerBuilder.PrimaryHandler = insecureHandler);
                    services.AddHttpClient("NormalClient");
                }).UseConsoleLifetime();

            var host = builder.Build();

            using var serviceScope = host.Services.CreateScope();
            var serviceProvider = serviceScope.ServiceProvider;
            var clientFactory = serviceProvider.GetService<IHttpClientFactory>();

#if DEBUG
            using var client = clientFactory.CreateClient("DebugClient");
#else
            using var client = clientFactory.CreateClient("NormalClient");
#endif

            // discover endpoints from metadata
            var disco = await client.GetDiscoveryDocumentAsync("https://192.168.1.46:5000");
            if (disco.IsError)
            {
                Console.WriteLine(disco.Error);
                return;
            }

            // request token
            var tokenResponse = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            {
                Address = disco.TokenEndpoint,

                ClientId = "client",
                ClientSecret = "secret",
                Scope = "api1"
            });

            if (tokenResponse.IsError)
            {
                Console.WriteLine(tokenResponse.Error);
                return;
            }

            // call api
            var apiClient = new HttpClient();
            apiClient.SetBearerToken(tokenResponse.AccessToken);

            var response = await apiClient.GetAsync("https://localhost:5001/identity");
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"{(int)response.StatusCode}: {response.StatusCode}");
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine(JArray.Parse(content));
            }
        }
    }
}