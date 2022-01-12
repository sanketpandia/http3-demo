using System.Net;

var client = new HttpClient();
client.DefaultRequestVersion = HttpVersion.Version30;
client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;

var resp =  await client.GetAsync("https://localhost:5001");
var body = await resp.Content.ReadAsStringAsync();

System.Console.WriteLine($"Status: {resp.StatusCode}, version: {resp.Version}");