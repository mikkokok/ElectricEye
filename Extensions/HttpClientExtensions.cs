using ElectricEye.Constants;
using ElectricEye.Helpers;

namespace ElectricEye.Extensions;

public static class HttpClientExtensions
{
    public static void AddHttpClients(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHttpClient(HttpClientConst.FalconClientName, (client) => { client.Timeout = TimeSpan.FromSeconds(30); })
            .ConfigurePrimaryHttpMessageHandler(provider =>
            {
                return new HttpClientHandler
                {
#pragma warning disable
                    ServerCertificateCustomValidationCallback = CertificateValidator.ValidateCertificate
                };
            });
    }
}