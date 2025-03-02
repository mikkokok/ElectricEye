using ElectricEye.Services;
using ElectricEye.Services.Clients;
using ElectricEye.Workers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<PricesClient>();
builder.Services.AddSingleton<RozalinaClient>();
builder.Services.AddSingleton<FalconClient>();
builder.Services.AddSingleton<ChargerClient>();
builder.Services.AddKeyedSingleton<ChargerService>("charger");
builder.Services.AddKeyedSingleton<PriceService>("price");
builder.Services.AddHostedService<ElectricEyeWorker>();

var app = builder.Build();


app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "ElectricEye API");
    options.RoutePrefix = string.Empty;
});

app.MapGet("/ping", () => "pong");
app.MapGet("/status", ([FromKeyedServices("charger")] ChargerService chargerService, [FromKeyedServices("price")] PriceService priceService) => Results.Ok(chargerService.GetStatus().Concat(priceService.GetStatus())));
app.MapGet("/prices/{current}", ([FromRoute]bool current, [FromKeyedServices("price")] PriceService priceService) =>
{
    if (current)
    {
        return Results.Ok(priceService.CurrentPrices);
    }
    return Results.Ok(priceService.TomorrowPrices);
});

app.Run();
