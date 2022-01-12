using System.Net;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) => cts.Cancel();

while (!cts.Token.IsCancellationRequested)
{
    using var client = new HttpClient();

    client.DefaultRequestVersion = HttpVersion.Version30;
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;

    var resp = await client.GetAsync("https://localhost:5001");
    var body = await resp.Content.ReadAsStringAsync();

    Console.WriteLine($"Status: {resp.StatusCode}, version: {resp.Version}");

    Console.WriteLine();
    Console.WriteLine();
    Console.WriteLine();
    Console.WriteLine("Waiting 1 second before next request ...");
    await Task.Delay(1000, cts.Token);
}
