namespace ElectricEye.Helpers.Impl
{
    public class ApikeyValidator : IApikeyValidator
    {
        private IConfiguration _config;
        private string _apikey;
        public ApikeyValidator(IConfiguration config)
        {
            _config = config;
            _apikey = _config["ApiKey"];
        }
        public bool ValidateApiKey(string apiKey)
        {
            return apiKey.Equals(_apikey);
        }
    }
}
