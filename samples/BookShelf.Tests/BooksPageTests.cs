namespace BookShelf.Tests;

public class BooksPageTests(BookShelfFactory factory)
    : IClassFixture<BookShelfFactory>
{
    [Fact]
    public async Task Index_returns_the_book_list()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/Books");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("The Hobbit", html);
    }

    [Fact]
    public async Task Create_adds_a_book()
    {
        var client = factory.CreateClient();
        var token = await Antiforgery.GetTokenAsync(client, "/Books/Create");

        var response = await client.PostAsync("/Books/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Title"] = "Piranesi",
            ["Author"] = "Susanna Clarke",
            ["Year"] = "2020"
        }));

        response.EnsureSuccessStatusCode();
        Assert.Contains("Piranesi", await response.Content.ReadAsStringAsync());
    }
}
