namespace ElectricEye.Routes
{
    public static partial class ApiMapper
    {
        public static void MapPingEndpoints(this WebApplication app)
        {
            var pingEndpoints = app.MapGroup("/ping")
                .WithTags("Ping");
            pingEndpoints.MapGet("/", () =>
            {
                return Results.Ok("Pong");
            }).WithName("Ping");
        }
    }
}