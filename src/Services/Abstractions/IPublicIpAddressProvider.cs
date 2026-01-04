using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace AzureDnsExternalIpSync.Cli.Services.Abstractions
{
    public interface IPublicIpAddressProvider
    {
        Task<IPAddress> GetPublicIpAddressAsync(CancellationToken cancellationToken);
    }
}
