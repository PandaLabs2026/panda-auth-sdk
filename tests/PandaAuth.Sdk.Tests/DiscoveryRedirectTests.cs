using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using PandaAuth.Sdk;
using Xunit;

namespace PandaAuth.Sdk.Tests;

public sealed class DiscoveryRedirectTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NativeTransportNeverFollowsDiscoveryOrTokenRedirect(bool redirectDiscovery)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, System.Security.Cryptography.HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var issuer = $"https://localhost:{((IPEndPoint)listener.LocalEndpoint).Port}/";
        var paths = new List<string>();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = Task.Run(async () => {
            try
            {
                while (true)
                {
                    using var tcp = await listener.AcceptTcpClientAsync(stop.Token);
                    using var tls = new SslStream(tcp.GetStream());
                    await tls.AuthenticateAsServerAsync(certificate);
                    using var reader = new StreamReader(tls, Encoding.ASCII, leaveOpen: true);
                    var line = await reader.ReadLineAsync(stop.Token);
                    var path = line!.Split(' ')[1];
                    paths.Add(path);
                    var length = 0;
                    while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(stop.Token)))
                        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) length = int.Parse(line.Split(':')[1]);
                    if (length > 0) await reader.ReadBlockAsync(new char[length], stop.Token);
                    var redirect = redirectDiscovery || path == "/token";
                    var body = redirect ? "" : JsonSerializer.Serialize(new { issuer, token_endpoint = issuer + "token" });
                    var status = redirect ? "307 Temporary Redirect" : "200 OK";
                    var response = $"HTTP/1.1 {status}\r\nLocation: {issuer}stolen\r\nContent-Type: application/json\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}";
                    await tls.WriteAsync(Encoding.UTF8.GetBytes(response), stop.Token);
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        });
        using var handler = new HttpClientHandler {
            UseProxy = false,
            ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert?.Thumbprint == certificate.Thumbprint
        };
        using var client = new PandaAuthClient(handler, new PandaAuthClientOptions { Issuer = issuer, ClientId = "fixture", ClientSecret = "synthetic-secret" });
        await Assert.ThrowsAsync<PandaAuthProtocolException>(() => client.GetClientCredentialsTokenAsync(stop.Token));
        stop.Cancel();
        await server;
        Assert.DoesNotContain("/stolen", paths);
        Assert.Equal(redirectDiscovery ? new[] { "/.well-known/openid-configuration" } : new[] { "/.well-known/openid-configuration", "/token" }, paths);
    }
}
