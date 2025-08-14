
namespace ElectricEye.Services
{
    public interface IRequestProvider
    {
        Task<TResult?> GetAsync<TResult>(string clientName, string url);
        Task PostAsync<TRequest>(string clientName, string url, TRequest data);
    }
}