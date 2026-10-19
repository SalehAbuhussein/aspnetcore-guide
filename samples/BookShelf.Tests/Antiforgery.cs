using System.Text.RegularExpressions;

namespace BookShelf.Tests;

public static partial class Antiforgery
{
    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>")]
    private static partial Regex TokenInputRegex();

    [GeneratedRegex("value=\"([^\"]+)\"")]
    private static partial Regex ValueRegex();

    public static async Task<string> GetTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var input = TokenInputRegex().Match(html);
        var value = ValueRegex().Match(input.Value);

        if (!input.Success || !value.Success)
            throw new InvalidOperationException($"No antiforgery token on {url}.");

        return value.Groups[1].Value;
    }
}
