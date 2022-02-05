using System;
using AzureDnsExternalIpSync.Cli.Options;
using AzureDnsExternalIpSync.Cli.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AzureDnsExternalIpSync.Cli
{
    public static class DependencyInjectionConfiguration
    {
        public static IServiceProvider Configure(IConfiguration configuration)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddOptions();
            serviceCollection.Configure<DnsResolverServiceOptions>(
                configuration.GetSection(DnsResolverServiceOptions.SectionName));
            serviceCollection.Configure<PrometheusOptions>(configuration.GetSection(PrometheusOptions.SectionName));
            serviceCollection.AddSingleton<DnsResolverService>();

            return serviceCollection.BuildServiceProvider();
        }
    }
}