using ElectricEye.Services;
using Microsoft.AspNetCore.Mvc;
namespace ElectricEye.Routes
{
    public static partial class ApiMapper
    {
        public static void MapPriceEndpoints(this WebApplication app)
        {
            var priceEndpoints = app.MapGroup("/prices")
                .WithTags("Prices");
            priceEndpoints.MapGet("/today", ([FromServices] PriceService priceService) =>
            {
                return Results.Ok(priceService.CurrentPrices);
            }).WithName("GetTodayPrices");
            priceEndpoints.MapGet("/tomorrow", ([FromServices] PriceService priceService) =>
            {
                return Results.Ok(priceService.TomorrowPrices);
            }).WithName("GetTomorrowPrices");
            priceEndpoints.MapGet("/updates", ([FromServices] PriceService priceService) =>
            {
                return Results.Ok(priceService.GetStatus());
            }).WithName("GetPriceServiceUpdatesList");
            priceEndpoints.MapGet("/status", ([FromServices] PriceService priceService) =>
            {
                return Results.Ok(new
                {
                    cleanTaskStatus = priceService.CleanTask?.Status.ToString() ?? "No cleanTask status",
                    cleanTaskExceptions = priceService.CleanTask?.Exception?.Message ?? "No cleanTask exceptions",
                    priceTaskStatus = priceService.PriceTask?.Status.ToString() ?? "No priceTask status",
                    PricePollingTaskExceptions = priceService.PriceTask?.Exception?.Message != null
                    ? [priceService.PriceTask.Exception.Message]
                    : Array.Empty<string>(),
                });
            }).WithName("GetPriceServiceStatus");
        }
    }
}
