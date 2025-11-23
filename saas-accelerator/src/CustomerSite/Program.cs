using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Marketplace.SaaS.Accelerator.CustomerSite;

/// <summary>
/// Program.
/// </summary>
public class Program
{
    /// <summary>
    /// Defines the entry point of the application.
    /// </summary>
    /// <param name="args">The arguments.</param>
    public static void Main(string[] args)
    {
        // LOCAL DEV MODE: Ignore SSL certificate validation errors for API emulator
        // The emulator uses HTTP but we configure it as HTTPS to bypass Azure SDK bearer token restrictions
        ServicePointManager.ServerCertificateValidationCallback =
            (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => true;

        CreateHostBuilder(args).Build().Run();
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddDebug()
                .AddConsole();
        });

        ILogger logger = loggerFactory.CreateLogger<Program>();
        logger.LogInformation("Service Provisioning initialized!!");
    }

    /// <summary>
    /// Creates the host builder.
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <returns> Host Builder.</returns>
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddDebug();
            })
            .ConfigureWebHostDefaults(webBuilder =>
            {
                //webBuilder.UseUrls("https://*:5001", "http://*:5000");
                webBuilder.UseStartup<Startup>();
            });
}