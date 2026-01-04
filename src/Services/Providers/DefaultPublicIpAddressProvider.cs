using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AzureDnsExternalIpSync.Cli.Services.Abstractions;

namespace AzureDnsExternalIpSync.Cli.Services.Providers
{
    public class DefaultPublicIpAddressProvider : IPublicIpAddressProvider
    {
        public async Task<IPAddress> GetPublicIpAddressAsync(CancellationToken cancellationToken)
        {
            var urlContent = await GetUrlContentAsStringAsync("https://ipv4.icanhazip.com/", cancellationToken);
            return ParseSingleIPv4Address(urlContent);
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

        private static async Task<string> GetUrlContentAsStringAsync(string url, CancellationToken cancellationToken)
        {
            using var httpClient = new HttpClient();
            using var httpResponse = await httpClient.GetAsync(url, cancellationToken);

            var urlContent =
                await httpResponse.Content.ReadAsStringAsync(cancellationToken);

            return urlContent;
        }
    }
}
