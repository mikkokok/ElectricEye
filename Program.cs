using ElectricEye.Helpers;
using ElectricEye.Helpers.Impl;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var falconConsumer = new FalconConsumer(builder.Configuration);
builder.Services.AddSingleton<IFalconConsumer>(falconConsumer);
var apiPoller = new ApiPoller(builder.Configuration, falconConsumer);
builder.Services.AddSingleton<IApiPoller>(apiPoller);


var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "ElectricEye API");
    options.RoutePrefix = string.Empty;
});

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
