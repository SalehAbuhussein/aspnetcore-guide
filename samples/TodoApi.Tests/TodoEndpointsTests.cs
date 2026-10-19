using System.Net;
using System.Net.Http.Json;
using TodoApi.Models;
using Xunit;

namespace TodoApi.Tests;

public class TodoEndpointsTests(TodoApiFactory factory)
    : IClassFixture<TodoApiFactory>
{
    [Fact]
    public async Task GetTodos_ReturnsOk()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/todos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // GET /todos returns a page since the Pagination and Querying pages.
        var todos = await response.Content.ReadFromJsonAsync<PagedResult<TodoResponse>>();
        Assert.NotNull(todos);
    }
}
