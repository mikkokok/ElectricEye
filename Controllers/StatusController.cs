using ElectricEye.Helpers;
using ElectricEye.Models;
using Microsoft.AspNetCore.Mvc;

namespace ElectricEye.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StatusController : Controller
    {
        private readonly IApiPoller _poller;
        private readonly IChargerPoller _chargerPoller;
        public StatusController(IApiPoller apiPoller, IChargerPoller chargerPoller)
        {
            _poller = apiPoller;
            _chargerPoller = chargerPoller;
        }

        [HttpGet]
        public List<PollerStatus> GetStatus()
        {
            return new List<PollerStatus> {
                _poller.GetStatus(),
                _chargerPoller.GetStatus(),
            };

        }

    }
}
