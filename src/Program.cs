using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AzureDnsExternalIpSync.Cli.Options;
using AzureDnsExternalIpSync.Cli.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using DotNetEnv;
using Prometheus;
using Serilog;

namespace AzureDnsExternalIpSync.Cli
{
    public static class Program
    {
        #region Env
        private static string FindEnvFile(string startPath)
        {
            try
            {
                var directory = new DirectoryInfo(startPath);
                string lastCheckedPath = null;

                while (directory != null)
                {
                    var envFile = Path.Combine(directory.FullName, ".env");
                    lastCheckedPath = directory.FullName;

                    // Check for .env file
                    if (File.Exists(envFile))
                    {
                        return envFile;
                    }

                    // Move to parent directory
                    directory = directory.Parent;

                    // Stop if we've reached the root
                    if (directory == null)
                    {
                        break;
                    }

                    // Stop if we haven't moved (shouldn't happen, but safety check)
                    if (lastCheckedPath == directory.FullName)
                    {
                        break;
                    }

                    // Stop if we've reached the filesystem root
                    if (directory.Parent == null)
                    {
                        break;
                    }
                }

                return null;
            }
            catch (Exception e)
            {
                Log.Logger.Warning(e, "Failed to recusively look for a .env file.");
                return null;
            }
        }

        private static void TryLoadEnvFile()
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var envPath = FindEnvFile(currentDirectory);

            if (envPath != null)
            {
                Env.Load(envPath);
                Log.Information("Loaded configuration from: {EnvPath}", envPath);
            }
            else
            {
                Log.Warning(".env file not found. Using system environment variables");
            }
        }
        #endregion

        private static readonly Dictionary<string, string> DefaultConfiguration = new Dictionary<string, string>
        {
            {$"{PrometheusOptions.SectionName}:{nameof(PrometheusOptions.Port)}", "9090"},
        };

        private static IConfiguration Configuration { get; set; }

        public static async Task<int> Main(string[] args)
        {
            TryLoadEnvFile();

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
                var server = new MetricServer("*", prometheusOptions.Value.Port);
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