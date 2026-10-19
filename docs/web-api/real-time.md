# Real-time with SignalR

## Introduction

Some clients need to know about changes the moment they happen, such as a shared todo board where two people work at once. Polling `GET /todos` every few seconds works, but it wastes requests and still lags. SignalR keeps a connection open and lets the server push messages to clients. If your clients only need fresh data when the user refreshes, you do not need it.

<ApiStyle />

## Setting up SignalR

SignalR is part of ASP.NET Core, so there is no server package to install.

```csharp [Hubs/TodoHub.cs]
using Microsoft.AspNetCore.SignalR;

namespace TodoApi.Hubs;

public class TodoHub : Hub
{
}
```

<div class="only-minimal">

```csharp [Program.cs (excerpt)]
using TodoApi.Hubs;

builder.Services.AddSignalR();

var app = builder.Build();

app.MapTodoEndpoints();
app.MapHub<TodoHub>("/hubs/todos");
```

</div>

<div class="only-controllers">

```csharp [Program.cs (excerpt)]
using TodoApi.Hubs;

builder.Services.AddControllers();
builder.Services.AddSignalR();

var app = builder.Build();

app.MapControllers();
app.MapHub<TodoHub>("/hubs/todos");
```

</div>

A hub is a class that clients connect to. `MapHub` exposes it at `/hubs/todos`. SignalR picks the best transport the client supports, usually WebSockets, and falls back to others when it has to.

## Pushing changes from endpoints

The hub does not need any methods for the server to send messages. Inject `IHubContext<TodoHub>` into a handler and send from there.

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
using Microsoft.AspNetCore.SignalR;
using TodoApi.Hubs;

static async Task<Results<NoContent, NotFound>> Update(
    int id, UpdateTodo input, AppDbContext db, IHubContext<TodoHub> hub)
{
    var todo = await db.Todos.FindAsync(id);
    if (todo is null)
    {
        return TypedResults.NotFound();
    }

    todo.Title = input.Title;
    todo.IsDone = input.IsDone;
    await db.SaveChangesAsync();

    await hub.Clients.All.SendAsync("TodoChanged", todo);

    return TypedResults.NoContent();
}
```

`IHubContext<TodoHub>` is registered by `AddSignalR`, so Minimal APIs inject it like any other service. `SendAsync("TodoChanged", todo)` calls the `TodoChanged` handler on every connected client and serializes `todo` as JSON. Send after `SaveChangesAsync`, so clients never hear about a change that failed to save. Do the same in `Create` and `Delete`.

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
using Microsoft.AspNetCore.SignalR;
using TodoApi.Hubs;

public class TodosController(AppDbContext db, IHubContext<TodoHub> hub) : ControllerBase
{
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateTodo input)
    {
        var todo = await db.Todos.FindAsync(id);
        if (todo is null)
        {
            return NotFound();
        }

        todo.Title = input.Title;
        todo.IsDone = input.IsDone;
        await db.SaveChangesAsync();

        await hub.Clients.All.SendAsync("TodoChanged", todo);

        return NoContent();
    }
}
```

`IHubContext<TodoHub>` is registered by `AddSignalR`, so the controller takes it as a constructor parameter like any other service. `SendAsync("TodoChanged", todo)` calls the `TodoChanged` handler on every connected client and serializes `todo` as JSON. Send after `SaveChangesAsync`, so clients never hear about a change that failed to save. Do the same in `Create` and `Delete`.

</div>

## A JavaScript client

```bash
npm install @microsoft/signalr
```

```js [src/todoHub.js]
import * as signalR from "@microsoft/signalr";

export async function connectToTodos(getToken, onChanged) {
  const connection = new signalR.HubConnectionBuilder()
    .withUrl("https://localhost:5001/hubs/todos", {
      accessTokenFactory: getToken,
    })
    .withAutomaticReconnect()
    .build();

  connection.on("TodoChanged", onChanged);

  await connection.start();
  return connection;
}
```

This works in Angular, React, Vue, or plain JavaScript. `connection.on` registers a handler for the message name the server sends. `withAutomaticReconnect` retries when the network drops. Call `connection.stop()` when the page or component goes away.

If the SPA runs on a different origin, your [CORS](/web-api/cors) policy must allow it, and it needs `AllowCredentials()` with an explicit origin list.

## Authentication

Browsers cannot set an `Authorization` header on a WebSocket. The SignalR client sends the token from `accessTokenFactory` as an `access_token` query string value instead, and you tell the JWT handler to read it there. Replace the `AddAuthentication().AddJwtBearer()` line in `Program.cs` with this version. Registering the handler twice fails at startup.

```csharp [Program.cs (excerpt)]
using Microsoft.AspNetCore.Authentication.JwtBearer;

builder.Services.AddAuthentication().AddJwtBearer(options =>
{
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var token = context.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(token) &&
                context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
            {
                context.Token = token;
            }

            return Task.CompletedTask;
        }
    };
});
```

`OnMessageReceived` runs before the handler looks for a token. The path check limits the query string token to hub requests, so normal endpoints still require the header. The rest of the settings still come from configuration, as on the [Authentication](/web-api/authentication) page.

```csharp [Hubs/TodoHub.cs (excerpt)]
using Microsoft.AspNetCore.Authorization;

[Authorize]
public class TodoHub : Hub
```

`[Authorize]` rejects connections without a valid token.

::: warning
Query strings can end up in server and proxy logs. Keep access tokens short-lived, and do not log full request URLs for `/hubs` paths.
:::

## Groups per project

Sending every change to every client does not scale past a small app. Groups let a client subscribe to one project.

```csharp [Hubs/TodoHub.cs]
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TodoApi.Hubs;

[Authorize]
public class TodoHub : Hub
{
    public Task JoinProject(int projectId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, $"project-{projectId}");

    public Task LeaveProject(int projectId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, $"project-{projectId}");
}
```

<div class="only-minimal">

```csharp [Endpoints/TodoEndpoints.cs (excerpt)]
if (todo.ProjectId is int projectId)
{
    await hub.Clients.Group($"project-{projectId}").SendAsync("TodoChanged", todo);
}
```

</div>

<div class="only-controllers">

```csharp [Controllers/TodosController.cs (excerpt)]
if (todo.ProjectId is int projectId)
{
    await hub.Clients.Group($"project-{projectId}").SendAsync("TodoChanged", todo);
}
```

</div>

```js [src/todoHub.js (excerpt)]
await connection.invoke("JoinProject", projectId);
```

`invoke` calls a public hub method from the client. A group is a named set of connections, and SignalR removes a connection from all groups when it disconnects. Check in `JoinProject` that the user may see the project before adding them, using the [authorization](/web-api/authorization) rules you already have.

## Scaling out

SignalR connections live in the memory of one server. With two or more instances behind a load balancer, a message sent on server A never reaches clients connected to server B. Fix this with a backplane: the `Microsoft.AspNetCore.SignalR.StackExchangeRedis` package and `AddSignalR().AddStackExchangeRedis(...)` relay messages through Redis, or Azure SignalR Service takes over the connections entirely. Most transports also need sticky sessions on the load balancer unless every client uses WebSockets with `skipNegotiation`.

## Learn more

- [Overview of ASP.NET Core SignalR](https://learn.microsoft.com/aspnet/core/signalr/introduction)
- [Authentication and authorization in ASP.NET Core SignalR](https://learn.microsoft.com/aspnet/core/signalr/authn-and-authz)
