using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace ElectricEye.Helpers
{
    public static class CertificateValidator
    {
        public static bool ValidateCertificate(HttpRequestMessage request, X509Certificate2 certificate, X509Chain certificateChain, SslPolicyErrors policy)
        {
            var certificate2 = new X509Certificate2(certificate);
            return certificate2.Thumbprint?.Equals(GlobalConfig.RestlessFalconConfig!.sslThumbprint, StringComparison.OrdinalIgnoreCase) == true;
        }
    }
}
