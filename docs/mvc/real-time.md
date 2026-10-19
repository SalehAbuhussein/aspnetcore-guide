# Real-time with SignalR

## Introduction

Some pages should update while the user is looking at them, such as a new review appearing on a book page without a refresh. SignalR keeps a live connection open between the browser and the server, so the server can push messages the moment something happens. Use it when users need to see changes within a second or two. If a refresh or a poll every minute is good enough, skip it and keep the app simpler.

## What SignalR is

SignalR is part of ASP.NET Core, so there is no server package to install. It picks the best transport the browser supports, usually WebSockets, and falls back to other techniques when it must. You write a hub, which is a class that clients connect to, and the server calls named methods on the connected clients.

This page pushes a `ReviewAdded` message to everyone viewing a book when someone posts a review. It builds on the `Review` model from the [Relationships](/mvc/relationships) page.

## Creating a hub

```csharp [Hubs/ReviewsHub.cs]
using Microsoft.AspNetCore.SignalR;

namespace BookShelf.Hubs;

public class ReviewsHub : Hub
{
    public Task JoinBook(int bookId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(bookId));

    public Task LeaveBook(int bookId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(bookId));

    public static string GroupName(int bookId) => $"book-{bookId}";
}
```

A group is a named set of connections. Each book gets its own group, so a review on Dune only reaches people looking at Dune. The browser calls `JoinBook` after it connects. `Context.ConnectionId` identifies the current connection, and SignalR removes it from all groups when it disconnects.

## Registering SignalR

```csharp [Program.cs (excerpt)]
using BookShelf.Hubs;

builder.Services.AddSignalR();

var app = builder.Build();

// ... existing middleware ...

app.MapHub<ReviewsHub>("/hubs/reviews");
```

`AddSignalR` registers the SignalR services. `MapHub` exposes the hub at `/hubs/reviews`. Put it next to `MapControllerRoute`, after `UseAuthorization`, so hub endpoints go through the same pipeline as your controllers.

## Pushing from a controller

A hub only runs while a client calls it. To send a message from anywhere else, such as a controller, inject `IHubContext<ReviewsHub>`.

```csharp [Controllers/ReviewsController.cs]
using BookShelf.Data;
using BookShelf.Hubs;
using BookShelf.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace BookShelf.Controllers;

public class ReviewsController(AppDbContext db, IHubContext<ReviewsHub> hub) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int bookId, int rating, string body)
    {
        if (rating is < 1 or > 5 || string.IsNullOrWhiteSpace(body))
        {
            return RedirectToAction("Details", "Books", new { id = bookId });
        }

        var review = new Review { BookId = bookId, Rating = rating, Body = body };
        db.Add(review);
        await db.SaveChangesAsync();

        await hub.Clients.Group(ReviewsHub.GroupName(bookId))
            .SendAsync("ReviewAdded", new { review.Id, review.Rating, review.Body });

        return RedirectToAction("Details", "Books", new { id = bookId });
    }
}
```

Send the message after `SaveChangesAsync` succeeds, so clients never see a review that failed to save. `SendAsync` takes the client method name and the arguments. SignalR serializes the object to JSON with camelCase names, so the browser receives `id`, `rating`, and `body`.

The person who posted the review also gets the message, because their page is in the group too. The redirect reloads their page anyway, so they see the review either way.

## The client in the Details view

The browser needs the SignalR JavaScript client. Load it from a CDN at the bottom of the Details view.

```razor [Views/Books/Details.cshtml]
@model BookShelf.Models.Book

<h1>@Model.Title</h1>
<p>@Model.Author, @Model.Year</p>

<h2>Reviews</h2>
<ul id="reviews">
    @foreach (var review in Model.Reviews)
    {
        <li>@review.Rating/5: @review.Body</li>
    }
</ul>

<form asp-controller="Reviews" asp-action="Create" asp-route-bookId="@Model.Id" method="post">
    <input name="rating" type="number" min="1" max="5" value="5" />
    <textarea name="body"></textarea>
    <button type="submit">Post review</button>
</form>

@section Scripts {
    <script src="https://cdn.jsdelivr.net/npm/@@microsoft/signalr@10.0.11/dist/browser/signalr.min.js"></script>
    <script>
        const bookId = @Model.Id;
        const list = document.getElementById("reviews");

        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/reviews")
            .withAutomaticReconnect()
            .build();

        connection.on("ReviewAdded", review => {
            const item = document.createElement("li");
            item.textContent = `${review.rating}/5: ${review.body}`;
            list.appendChild(item);
        });

        connection.onreconnected(() => connection.invoke("JoinBook", bookId));

        connection.start()
            .then(() => connection.invoke("JoinBook", bookId))
            .catch(err => console.error(err));
    </script>
}
```

In Razor, `@@` writes a literal `@`, which the npm package name needs. `connection.on` registers a handler for `ReviewAdded`. `invoke` calls a hub method on the server. `textContent` inserts the review as plain text, so a review body cannot inject HTML.

The Details action must load reviews for the loop to work, for example with `db.Books.Include(b => b.Reviews).FirstOrDefaultAsync(b => b.Id == id)`. The `Scripts` section renders where the default layout calls `RenderSectionAsync("Scripts", required: false)`.

::: warning
Group membership does not survive a reconnect, because the new connection gets a new id. The `onreconnected` handler joins the group again. Without it, a user whose Wi-Fi drops for a moment stops getting updates.
:::

## Securing the hub

Hubs accept `[Authorize]` the same way controllers do. Put it on `ReviewsHub` when only signed-in users should connect. Inside the hub, `Context.User` gives you the current user, which is useful when you want a private group per user.

## Scaling out

Each server keeps its own list of connections and groups. If you run two copies of BookShelf behind a load balancer, a message sent on server A never reaches a browser connected to server B. Fix this with a backplane, which is a shared channel that forwards messages between servers. Add the `Microsoft.AspNetCore.SignalR.StackExchangeRedis` package and call `.AddStackExchangeRedis(connectionString)` after `AddSignalR()`, or move the connections to Azure SignalR Service, which handles them for you. Both options also need sticky sessions or WebSockets-only connections, so check the scale-out guide before you go live.

## Learn more

- [Overview of ASP.NET Core SignalR](https://learn.microsoft.com/aspnet/core/signalr/introduction)
- [ASP.NET Core SignalR hosting and scaling](https://learn.microsoft.com/aspnet/core/signalr/scale)
