using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AzureDnsExternalIpSync.Cli.Options;
using AzureDnsExternalIpSync.Cli.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Prometheus;
using Serilog;

namespace AzureDnsExternalIpSync.Cli
{
    public static class Program
    {
        private static readonly Dictionary<string, string> DefaultConfiguration = new Dictionary<string, string>
        {
            {$"{PrometheusOptions.SectionName}:{nameof(PrometheusOptions.Port)}", "9090"},
        };

        private static IConfiguration Configuration { get; set; }

        public static async Task<int> Main(string[] args)
        {
            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder
                .AddInMemoryCollection(DefaultConfiguration)
                .AddJsonFile("appsettings.json", false, false)
                .AddEnvironmentVariables();

            Configuration = configurationBuilder.Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(Configuration)
                .MinimumLevel.Information()
                .WriteTo.Console() // Always write to console.
                .CreateLogger();

            // Support CTRL-C
            using var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, arguments) =>
            {
                arguments.Cancel = true;

                // ReSharper disable once AccessToDisposedClosure | endless loop follows.
                cancellationTokenSource.Cancel();
            };

            // Configure Simple DI (binds options and turns them injectable).
            var serviceProvider = DependencyInjectionConfiguration.Configure(Configuration);

            try
            {
                var prometheusOptions = serviceProvider.GetRequiredService<IOptions<PrometheusOptions>>();
                Log.Information("Starting Prometheus Listener on port {@Port}...", prometheusOptions.Value.Port);

                // Prometheus Scrap API.
                var server = new MetricServer("localhost", prometheusOptions.Value.Port);
                server.Start();
                Log.Information("Prometheus Listener started successfully.");
                
                Log.Information("Starting (endless) DnsResolverService loop...");
                var service = serviceProvider.GetService<DnsResolverService>();
                await service.Run(cancellationTokenSource.Token);
                return 0;
            }
            catch (TaskCanceledException)
            {
                Log.Warning("Cancelled by user.");
                return 1;
            }
            catch (Exception exception)
            {
                Log.Fatal(exception, "Fatal error.");
                return 2;
            }
        }
    }
}