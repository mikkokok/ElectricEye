using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace ElectricEye.Helpers
{
    public class CertificateValidator
    {
        private IConfiguration _config;
        private readonly string _trustedThumbprint;

        public CertificateValidator(IConfiguration config)
        {
            _config = config;
            _trustedThumbprint = _config["RestlessFalcon:sslThumbprint"] ?? throw new Exception($"CertificateValidator initialisation failed due to RestlessFalcon:sslThumbprint config being null"); ;
        }

        public bool ValidateCertificate(HttpRequestMessage request, X509Certificate2 certificate, X509Chain certificateChain, SslPolicyErrors policy)
        {
            var certificate2 = new X509Certificate2(certificate);
            return certificate2.Thumbprint?.Equals(_trustedThumbprint, StringComparison.OrdinalIgnoreCase) == true;
        }
    }
}
