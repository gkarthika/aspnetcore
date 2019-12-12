using System;
using System.Buffers;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Logging;

namespace QuicSampleApp
{
    public class Startup
    {
        public void Configure(IApplicationBuilder app)
        {
            app.Run((httpContext) =>
            {
                return Task.CompletedTask;
            });
        }

        public static void Main(string[] args)
        {
            var cert = CertificateLoader.LoadFromStoreCert("localhost", StoreName.My.ToString(), StoreLocation.CurrentUser, true);
            var hostBuilder = new WebHostBuilder()
                 .ConfigureLogging((_, factory) =>
                 {
                     factory.SetMinimumLevel(LogLevel.Debug);
                     factory.AddConsole();
                 })
                 .UseKestrel()
                 .UseMsQuic(options =>
                 {
                     options.Certificate = cert;
                     options.RegistrationName = "AspNetCore-MsQuic";
                     options.Alpn = "QuicTest";
                     options.IdleTimeout = TimeSpan.FromHours(1);
                 })
                 .ConfigureKestrel((context, options) =>
                 {
                     var basePort = 5555;

                     options.Listen(IPAddress.Any, basePort, listenOptions =>
                     {
                         listenOptions.Protocols = HttpProtocols.Http3;

                         async Task EchoServer(MultiplexedConnectionContext connection)
                         {
                             // For graceful shutdown

                             while (true)
                             {
                                 var stream = await connection.AcceptAsync();
                                 try
                                 {
                                     while (true)
                                     {
                                         var result = await stream.Transport.Input.ReadAsync();

                                         if (result.IsCompleted)
                                         {
                                             break;
                                         }

                                         await stream.Transport.Output.WriteAsync(result.Buffer.ToArray());

                                         stream.Transport.Input.AdvanceTo(result.Buffer.End);
                                     }
                                 }
                                 catch (OperationCanceledException)
                                 {
                                 }
                             }
                         }

                         listenOptions.RunMultiplexed(EchoServer);
                     });
                 })
                 .UseStartup<Startup>();

            hostBuilder.Build().Run();
        }
    }
}