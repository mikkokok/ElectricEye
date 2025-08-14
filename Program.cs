using ElectricEye.Extensions;
using ElectricEye.Helpers;
using ElectricEye.Services;
using ElectricEye.Workers;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

GlobalConfig.ApiKey = builder.Configuration["ApiKey"]!;
GlobalConfig.ChargerUrl = builder.Configuration["ChargerUrl"]!;
GlobalConfig.PricesAPIConfig = builder.Configuration.GetRequiredSection("PricesAPI").Get<GlobalConfig.PricesAPI>()!;
GlobalConfig.RestlessFalconConfig = builder.Configuration.GetRequiredSection("RestlessFalcon").Get<GlobalConfig.RestlessFalcon>()!;
GlobalConfig.TelegramAPIConfig = builder.Configuration.GetRequiredSection("TelegramAPI").Get<GlobalConfig.TelegramAPI>()!;

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IRequestProvider, RequestProvider>();
builder.Services.AddKeyedSingleton<ChargerService>("charger");
builder.Services.AddKeyedSingleton<PriceService>("price");
builder.Services.AddHostedService<ElectricEyeWorker>();

builder.AddHttpClients();

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