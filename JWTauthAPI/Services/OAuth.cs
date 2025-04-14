using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

public interface IOAuthService
{
    Task<string?> GetAccessTokenFromLifeCapital(string code);
    Task<UserInfo?> GetUserInfoFromLifeCapital(string accessToken);
}

public class OAuthService : IOAuthService
{
    private readonly IConfiguration _configuration;

    public OAuthService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<string?> GetAccessTokenFromLifeCapital(string code)
    {
        var clientId = _configuration["OAuth:ClientId"];
        var clientSecret = _configuration["OAuth:ClientSecret"];
        var redirectUri = _configuration["OAuth:RedirectUri"];

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) 
			|| string.IsNullOrEmpty(redirectUri))
        {
            throw new InvalidOperationException("OAuth configuration missing.");
        }

        var tokenUrl = "https://mail.lifecapital.eg/oauth/token";
        var requestData = new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "code", code },
            { "redirect_uri", redirectUri },
            { "client_id", clientId },
            { "client_secret", clientSecret }
        };

        using (var client = new HttpClient())
        {
            var response = await client.PostAsync(tokenUrl, new FormUrlEncodedContent(requestData));
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<Dictionary<string, string>>(responseContent);
            return tokenResponse?.GetValueOrDefault("access_token");
        }
    }

    public async Task<UserInfo?> GetUserInfoFromLifeCapital(string accessToken)
    {
        var userInfoUrl = "https://mail.lifecapital.eg/oauth/userinfo";

        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await client.GetAsync(userInfoUrl);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<UserInfo>(responseContent);
        }
    }
}

public class UserInfo
{
    public string? Username { get; set; }
    public string? Email { get; set; }
}
