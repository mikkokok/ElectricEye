using ElectricEye.Services;
using Microsoft.AspNetCore.Mvc;

namespace ElectricEye.Routes
{
    public static partial class ApiMapper
    {
        public static void MapChargerEndpoints(this WebApplication app)
        {
            var chargerEndpoints = app.MapGroup("/charger")
                .WithTags("Chargers");
            chargerEndpoints.MapGet("/updates", ([FromServices] ChargerService chargerService) =>
            {
                var status = chargerService.GetStatus();
                return Results.Ok(status);
            });
            chargerEndpoints.MapGet("/status", ([FromServices] ChargerService chargerService) =>
            {
                return Results.Ok(new
                {
                    cleanTaskStatus = chargerService.CleanTask?.Status.ToString() ?? "No cleanTask status",
                    cleanTaskExceptions = chargerService.CleanTask?.Exception?.Message ?? "No cleanTask exceptions",
                    chargerTaskStatus = chargerService.ChargerTask?.Status.ToString() ?? "No chargerTask status",
                    ChargerPollingTaskExceptions = chargerService.ChargerTask?.Exception?.Message != null
                    ? [chargerService.ChargerTask.Exception.Message]
                    : Array.Empty<string>(),
                });
            }).WithName("GetChargerServiceStatus");
        }
    }
}