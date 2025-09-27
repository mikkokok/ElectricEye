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
builder.Services.AddSingleton<ChargerService>();
builder.Services.AddSingleton<PriceService>();
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
app.MapGet("/status", ([FromServices] ChargerService chargerService, [FromServices] PriceService priceService) => Results.Ok(chargerService.GetStatus().Concat(priceService.GetStatus())));
app.MapGet("/prices/{current}", ([FromRoute]bool current, [FromServices] PriceService priceService) =>
{
    if (current)
    {
        return Results.Ok(priceService.CurrentPrices);
    }
    return Results.Ok(priceService.TomorrowPrices);
});
app.MapGet("/tasks", ([FromServices] ChargerService chargerService, [FromServices] PriceService priceService) =>
{
    return Results.Ok(new
    {
        ChargerService = chargerService.CleanTask?.Status.ToString() ?? "No cleaning task",
        ChargerServiceExceptions = chargerService.CleanTask?.Exception?.Message != null
            ? [chargerService.CleanTask.Exception.Message]
            : Array.Empty<string>(),
        PriceService = priceService.CleanTask?.Status.ToString() ?? "No cleaning task",
        PriceServiceExceptions = priceService.CleanTask?.Exception?.Message != null
            ? [priceService.CleanTask.Exception.Message]
            : Array.Empty<string>(),
        PricePollingTask = priceService.PriceTask?.Status.ToString() ?? "No price polling task",
        PricePollingTaskExceptions = priceService.PriceTask?.Exception?.Message != null
            ? [priceService.PriceTask.Exception.Message]
            : Array.Empty<string>(),
        ChargerPollingTask = chargerService.ChargerTask?.Status.ToString() ?? "No charger polling task",
        ChargerPollingTaskExceptions = chargerService.ChargerTask?.Exception?.Message != null
            ? [chargerService.ChargerTask.Exception.Message]
            : Array.Empty<string>(),
    });
});

app.Run();