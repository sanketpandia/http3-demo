using System.Net;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel((context, options) => 
{
    options.Listen(IPAddress.Loopback,5001, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
        listenOptions.UseHttps();
    });
});

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.MapPost("/upload", async Task<IResult>(IFormFile request) =>
    {
        if (request.Length == 0)
            return Results.BadRequest();

        await using var stream = request.OpenReadStream();

        var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync();
        System.Console.WriteLine("=====================================\nPOST Request Received\n=======================================");
        return Results.Ok(text);
    });
app.Run();
