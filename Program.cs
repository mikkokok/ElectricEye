using ElectricEye.Helpers;
using ElectricEye.Helpers.Impl;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var apiPoller = new ApiPoller(builder.Configuration);
builder.Services.AddSingleton<IApiPoller>(apiPoller);
var falconConsumer = new FalconConsumer(builder.Configuration);
builder.Services.AddSingleton<IFalconConsumer>(falconConsumer);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    options.RoutePrefix = string.Empty;
});

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
