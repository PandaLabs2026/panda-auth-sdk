using PandaAuth.Sdk;

var client = new PandaAuthClient(new HttpClient(), new PandaAuthClientOptions
{
    Issuer = Required("PANDA_AUTH_ISSUER"),
    ClientId = Required("PANDA_AUTH_CLIENT_ID"),
    ClientSecret = Required("PANDA_AUTH_CLIENT_SECRET"),
    Scopes = ["api"]
});

var token = await client.GetClientCredentialsTokenAsync();

Console.WriteLine($"Client credentials succeeded; token_type={token.TokenType}, expires_in={token.ExpiresIn}s.");
Console.WriteLine("The sample deliberately does not print or persist the access token.");

static string Required(string name)
    => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException($"Set {name} before running this sample.");
