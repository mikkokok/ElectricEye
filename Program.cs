using ElectricEye.Extensions;
using ElectricEye.Helpers;
using ElectricEye.Routes;
using ElectricEye.Routes.Middlewares;
using ElectricEye.Services;
using ElectricEye.Workers;

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

app.UseRouting();
app.UseMiddleware<ApiVersionHeaderMiddleware>();
app.MapPriceEndpoints();
app.MapChargerEndpoints();
app.MapPingEndpoints();

app.Run();