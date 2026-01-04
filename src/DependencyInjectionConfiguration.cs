using System;
using AzureDnsExternalIpSync.Cli.Options;
using AzureDnsExternalIpSync.Cli.Services;
using AzureDnsExternalIpSync.Cli.Services.Abstractions;
using AzureDnsExternalIpSync.Cli.Services.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AzureDnsExternalIpSync.Cli
{
    public static class DependencyInjectionConfiguration
    {
        public static IServiceProvider Configure(IConfiguration configuration)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();
            serviceCollection.AddOptions();
            serviceCollection.Configure<DnsResolverServiceOptions>(
                configuration.GetSection(DnsResolverServiceOptions.SectionName));
            serviceCollection.Configure<PrometheusOptions>(configuration.GetSection(PrometheusOptions.SectionName));
            serviceCollection.Configure<AwsLambdaOptions>(configuration.GetSection(AwsLambdaOptions.SectionName));

            var awsLambdaOptions = configuration.GetSection(AwsLambdaOptions.SectionName).Get<AwsLambdaOptions>();
            if (!string.IsNullOrEmpty(awsLambdaOptions?.FunctionUrl))
            {
                serviceCollection.AddSingleton<IPublicIpAddressProvider, AwsLambdaPublicIpAddressProvider>();
            }
            else
            {
                serviceCollection.AddSingleton<IPublicIpAddressProvider, DefaultPublicIpAddressProvider>();
            }

            serviceCollection.AddSingleton<DnsResolverService>();

            return serviceCollection.BuildServiceProvider();
        }
    }
}