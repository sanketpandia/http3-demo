using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) => cts.Cancel();

while (!cts.Token.IsCancellationRequested)
{
    using var handler = new HttpClientHandler();
    handler.ServerCertificateCustomValidationCallback = ServerCertificateCustomValidation;

    using var client = new HttpClient(handler);

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

    bool ServerCertificateCustomValidation(HttpRequestMessage requestMessage, X509Certificate2? certificate, X509Chain? chain, SslPolicyErrors sslErrors)
    {
        // It is possible inpect the certificate provided by server
        Console.WriteLine($"Requested URI: {requestMessage.RequestUri}");
        Console.WriteLine($"Effective date: {certificate?.GetEffectiveDateString()}");
        Console.WriteLine($"Exp date: {certificate?.GetExpirationDateString()}");
        Console.WriteLine($"Issuer: {certificate?.Issuer}");
        Console.WriteLine($"Subject: {certificate?.Subject}");

        // Based on the custom logic it is possible to decide whether the client considers certificate valid or not
        Console.WriteLine("========================================");
        var consoleColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Errors: {sslErrors}");
        Console.ForegroundColor = consoleColor;
        Console.WriteLine("========================================");

        // return sslErrors == SslPolicyErrors.None;
        return true;
    }
}
