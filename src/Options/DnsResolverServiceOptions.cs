using AzureDnsExternalIpSync.Cli.Services;

namespace AzureDnsExternalIpSync.Cli.Options
{
    public class DnsResolverServiceOptions
    {
        public const string SectionName = nameof(DnsResolverService);

        public int UpdateFrequencyInMinutes { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string TenantId { get; set; }
        public string SubscriptionId { get; set; }
        public string ResourceGroupName { get; set; }
        public string AzureDnsZoneName { get; set; }
        public string AzureDnsRecordSetName { get; set; }
        public string Host { get; set; }
    }
}