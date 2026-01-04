using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Dns;
using Azure.ResourceManager.Dns.Models;
using AzureDnsExternalIpSync.Cli.Options;
using AzureDnsExternalIpSync.Cli.Services.Abstractions;
using Microsoft.Extensions.Options;
using Serilog;

namespace AzureDnsExternalIpSync.Cli.Services
{
    public class DnsResolverService
    {
        private readonly IOptions<DnsResolverServiceOptions> _options;
        private readonly IPublicIpAddressProvider _ipProvider;

        public DnsResolverService(IOptions<DnsResolverServiceOptions> options, IPublicIpAddressProvider ipProvider)
        {
            _options = options;
            _ipProvider = ipProvider;
        }

        public async Task Run(CancellationToken cancellationToken)
        {
            Log.Information("Update frequency set to {@UpdateFrequency} min.", _options.Value.UpdateFrequencyInMinutes);
            
            var credential = new ClientSecretCredential(_options.Value.TenantId, _options.Value.ClientId, _options.Value.ClientSecret);
            var armClient = new ArmClient(credential);
            var dnsZoneResourceId = DnsZoneResource.CreateResourceIdentifier(_options.Value.SubscriptionId, _options.Value.ResourceGroupName, _options.Value.AzureDnsZoneName);
            var dnsZone = armClient.GetDnsZoneResource(dnsZoneResourceId);
            
            while (!cancellationToken.IsCancellationRequested)
            {
                var externalIpAddress = await _ipProvider.GetPublicIpAddressAsync(cancellationToken);
                var aRecordCollection = dnsZone.GetDnsARecords();
                DnsARecordData recordData = new DnsARecordData();
                recordData.TtlInSeconds = 3600;

                try 
                {
                    var existingRecord = await aRecordCollection.GetAsync(_options.Value.AzureDnsRecordSetName, cancellationToken: cancellationToken);
                    if (existingRecord.HasValue)
                    {
                        recordData = existingRecord.Value.Data;
                        recordData.DnsARecords.Clear();
                    }
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    // Record doesn't exist, use default
                }

                recordData.DnsARecords.Add(new DnsARecordInfo { IPv4Address = externalIpAddress });

                await aRecordCollection.CreateOrUpdateAsync(WaitUntil.Completed, _options.Value.AzureDnsRecordSetName, recordData, cancellationToken: cancellationToken);

                var dnsEntry = $"{_options.Value.AzureDnsRecordSetName}.{_options.Value.Host}";
                Log.Information("Successfully updated Azure DNS {@dnsEntry} to {@ipaddress}.", dnsEntry, externalIpAddress.ToString());
                
                for (int i = _options.Value.UpdateFrequencyInMinutes; i > 0; i--)
                {
                    Log.Information("{@minutes} min to next update cycle...", i);
                    
                    // 1min "async sleep"
                    await Task.Delay(1000 * 60, cancellationToken);
                }
            }
        }
    }
}