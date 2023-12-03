using ElectricEye.Helpers;
using ElectricEye.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ElectricEye.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PricesController: ControllerBase
    {
        private readonly IApiPoller poller;

        public PricesController(IApiPoller apiPoller)
        {
            poller = apiPoller;
        }
        [HttpGet]
        public List<ElectricityPrice> GetPrices(bool current = true)
        {
            if (current)
            {
                return poller.CurrentPrices;
            }
            return poller.TomorrowPrices;
            
        }
    }
}
