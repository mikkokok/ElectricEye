using ElectricEye.Helpers;
using ElectricEye.Services.Clients;

namespace ElectricEye.Extensions;

public static class HttpClientExtensions
{

    public static void AddHttpClients(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHttpClient("default", client => { client.Timeout = TimeSpan.FromSeconds(30); });

        builder.Services.AddHttpClient(nameof(FalconClient), (client) =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .ConfigurePrimaryHttpMessageHandler(provider =>
        {
            var config = provider.GetRequiredService<IConfiguration>();
            var certificateValidator = new CertificateValidator(config);
            return new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = certificateValidator.ValidateCertificate
            };
        });
    }
}

public class HttpClientConst
{
    public const string DEFAULT_CLIENT_NAME = "default";
}