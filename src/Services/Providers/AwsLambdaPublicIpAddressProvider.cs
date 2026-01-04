using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.Runtime.Internal.Auth;
using Amazon.Util;
using AzureDnsExternalIpSync.Cli.Options;
using AzureDnsExternalIpSync.Cli.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace AzureDnsExternalIpSync.Cli.Services.Providers
{
    public class AwsLambdaPublicIpAddressProvider : IPublicIpAddressProvider
    {
        private readonly AwsLambdaOptions _options;

        public AwsLambdaPublicIpAddressProvider(IOptions<AwsLambdaOptions> options)
        {
            _options = options.Value;
        }

        public async Task<IPAddress> GetPublicIpAddressAsync(CancellationToken cancellationToken)
        {
            var responseJson = await InvokeLambdaAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.TryGetProperty("ip", out var ipElement))
            {
                var ipString = ipElement.GetString();
                if (IPAddress.TryParse(ipString, out var ipAddress))
                {
                    return ipAddress;
                }
            }
            
            throw new Exception($"Failed to parse IP address from Lambda response: {responseJson}");
        }

        private class GenericRequest : AmazonWebServiceRequest
        {
        }

        private class LambdaClientConfig : ClientConfig
        {
            public LambdaClientConfig(string region)
            {
                this.RegionEndpoint = RegionEndpoint.GetBySystemName(region);
            }

            public override string ServiceVersion => "2015-03-31";
            public override string UserAgent => "AzureDnsExternalIpSync";
            public override string RegionEndpointServiceName => "lambda";
        }

        private async Task<string> InvokeLambdaAsync(CancellationToken cancellationToken)
        {
            var credentials = new BasicAWSCredentials(_options.AccessKeyId, _options.SecretAccessKey);
            var uri = new Uri(_options.FunctionUrl);
            var request = new DefaultRequest(new GenericRequest(), "lambda")
            {
                HttpMethod = "GET",
                Endpoint = uri,
                ResourcePath = uri.AbsolutePath,
                AuthenticationRegion = _options.Region
            };

            var immutableCredentials = await credentials.GetCredentialsAsync();
            var config = new LambdaClientConfig(_options.Region);
            new AWS4Signer().Sign(request, config, null, immutableCredentials.AccessKey, immutableCredentials.SecretKey);

            using var httpClient = new HttpClient();
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, _options.FunctionUrl);

            foreach (var header in request.Headers)
            {
                httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            var response = await httpClient.SendAsync(httpRequest, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
    }
}
