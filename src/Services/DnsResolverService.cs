using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using AzureDnsExternalIpSync.Cli.Options;
using Microsoft.Azure.Management.Dns;
using Microsoft.Azure.Management.Dns.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest.Azure.Authentication;
using Serilog;

namespace AzureDnsExternalIpSync.Cli.Services
{
    public class DnsResolverService
    {
        private readonly IOptions<DnsResolverServiceOptions> _options;

        public DnsResolverService(IOptions<DnsResolverServiceOptions> options)
        {
            _options = options;
        }

        private static IPAddress ParseSingleIPv4Address(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                throw new ArgumentException("Input string must not be null", input);
            }

            var addressBytesSplit = input.Trim().Split('.').ToList();
            if (addressBytesSplit.Count != 4)
            {
                throw new ArgumentException("Input string was not in valid IPV4 format \"a.b.c.d\"", input);
            }

            var addressBytes = new byte[4];
            foreach (var i in Enumerable.Range(0, addressBytesSplit.Count))
            {
                if (!int.TryParse(addressBytesSplit[i], out var parsedInt))
                {
                    throw new FormatException($"Unable to parse integer from {addressBytesSplit[i]}");
                }

                if (0 > parsedInt || parsedInt > 255)
                {
                    throw new ArgumentOutOfRangeException($"{parsedInt} not within required IP address range [0,255]");
                }

                addressBytes[i] = (byte)parsedInt;
            }

            return new IPAddress(addressBytes);
        }

        private static async Task<IPAddress> GetPublicIPv4AddressAsync(CancellationToken cancellationToken)
        {
            var urlContent =
                await GetUrlContentAsStringAsync("http://ipv4.icanhazip.com/", cancellationToken);

            return ParseSingleIPv4Address(urlContent);
        }

        private static async Task<string> GetUrlContentAsStringAsync(string url, CancellationToken cancellationToken)
        {
            using var httpClient = new HttpClient();
            using var httpResponse = await httpClient.GetAsync(url, cancellationToken);

            var urlContent =
                await httpResponse.Content.ReadAsStringAsync(cancellationToken);

            return urlContent;
        }

        public async Task Run(CancellationToken cancellationToken)
        {
            Log.Information("Update frequency set to {@UpdateFrequency} min.", _options.Value.UpdateFrequencyInMinutes);
            
            var credentials = await ApplicationTokenProvider.LoginSilentAsync(_options.Value.TenantId, _options.Value.ClientId, _options.Value.ClientSecret);
            var dnsClient = new DnsManagementClient(credentials);
            dnsClient.SubscriptionId = _options.Value.SubscriptionId;
            
            while (!cancellationToken.IsCancellationRequested)
            {
                var externalIpAddress = await GetPublicIPv4AddressAsync(cancellationToken);
                var recordSet = new RecordSet(aRecords: new List<ARecord>(new[]
                    { new ARecord(ipv4Address: externalIpAddress.ToString()) }));
                
                Log.Information("Current External IP Address: {@ipaddress}", externalIpAddress.ToString());
                Log.Information("Updating Azure DNS entry...");
                Log.Information("Successfully updated Azure DNS entry.");

                await dnsClient.RecordSets.UpdateAsync(
                    _options.Value.ResourceGroupName,
                    _options.Value.AzureDnsZoneName,
                    _options.Value.AzureDnsRecordSetName,
                    RecordType.A,
                    recordSet,
                    cancellationToken: cancellationToken);
                
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