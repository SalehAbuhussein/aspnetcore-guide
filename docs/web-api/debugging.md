# Debugging

## Introduction

A debugger lets you pause the API on any line, look at every variable, and step through the code one line at a time. This page shows how to do that in VS Code and Visual Studio, how to read the error details ASP.NET Core returns in Development, and how to look inside the TodoApi database.

## Start the debugger in VS Code

Install the C# Dev Kit extension, then open the TodoApi folder. Open `Program.cs` and press F5. The first time, VS Code asks which debugger to use. Pick C#, then pick the `http` launch profile. The API builds and starts on `http://localhost:5000`.

You can also open the Run and Debug view from the side bar and click the green Run button. VS Code saves your choice in `.vscode/launch.json`, so the next F5 starts straight away. Press Shift+F5 to stop.

## Breakpoints

Click in the gutter to the left of a line number in `Endpoints/TodoEndpoints.cs`. A red dot appears. That is a breakpoint. Send `GET /todos` from `TodoApi.http` or curl, and VS Code pauses on that line before it runs.

While paused, you can:

- Hover over a variable to see its value. The Variables panel lists every local variable.
- Press F10 to step over the current line, F11 to step into a method call, and F5 to continue.
- Add an expression such as `todo.Title` to the Watch panel. It updates after every step.
- Read the Call Stack panel. It lists the chain of methods that led to this line, newest at the top. Click a row to jump to that method.

Right-click a breakpoint and pick Edit Breakpoint to make it conditional. A condition such as `id == 5` pauses only when it is true, which saves you from clicking Continue fifty times in a loop.

## Visual Studio

Open `TodoApi.slnx` and press F5. Breakpoints, stepping keys, Watch, and Call Stack work the same way as in VS Code. Right-click a breakpoint and choose Conditions for a conditional breakpoint.

## dotnet watch or the debugger

`dotnet watch` rebuilds and restarts the API each time you save a file. Use it while you add endpoints, because you can call the new code within seconds. It does not pause on breakpoints. Start the debugger when you need to know why something happens, such as a value that is `null` when it should not be. You cannot run both on the same port at once, so stop one before you start the other.

## Exception details in Development

When your code throws an exception in Development, ASP.NET Core returns a `500` with the details inside a ProblemDetails body, the standard JSON error shape from [HTTP basics](/web-api/http-basics). This is real output, shortened:

```http
HTTP/1.1 500 Internal Server Error
Content-Type: application/problem+json

{"type":"https://tools.ietf.org/html/rfc9110#section-15.6.1","title":"System.InvalidOperationException","status":500,"detail":"Something broke","exception":{"details":"System.InvalidOperationException: Something broke\r\n   at Program.<>c.<<Main>$>b__0_8() in C:\\src\\TodoApi\\Program.cs:line 143\r\n ...","headers":{...},"path":"/boom","endpoint":"HTTP: GET /boom",...}}
```

The same stack trace also appears in the terminal where the API runs. Outside Development, the `exception` part is left out and clients get a plain `500` from the handler on the [Error handling](/web-api/errors) page, because the details can leak secrets.

::: warning
Never show exception details in Production. They include source file paths and request headers.
:::

## Reading a stack trace

```text
System.InvalidOperationException: Sequence contains no elements.
   at System.Linq.ThrowHelper.ThrowNoElementsException()
   at System.Linq.Enumerable.First[TSource](IEnumerable`1 source)
   at TodoApi.Endpoints.TodoEndpoints.GetStats(AppDbContext db) in C:\src\TodoApi\Endpoints\TodoEndpoints.cs:line 88
   at lambda_method57(Closure, Object, HttpContext)
   at Microsoft.AspNetCore.Authorization.AuthorizationMiddleware.Invoke(HttpContext context)
```

The first line names the exception type and its message. Each line after it is one method call, with the method that threw at the top. Skip the `System` and `Microsoft` lines and find the first line from your own code, here `GetStats` at line 88. Put a breakpoint there and send the request again.

## Logging as debugging

Sometimes pausing the API is not practical, for example when a bug only shows up on a server. Write log messages with `ILogger<T>` instead and read them in the terminal. Raise a category to `Debug` in `appsettings.Development.json` to see more detail. The [Logging](/web-api/logging) page covers levels, structured messages, and HTTP logging.

## Look inside the database

Once you add a database on the [Database](/web-api/database) page, TodoApi stores its data in `todos.db` in the project folder. In VS Code, install a SQLite viewer extension such as SQLite Viewer, then click the file to browse tables. From a terminal, use the `sqlite3` tool:

```bash
sqlite3 todos.db
sqlite> .tables
sqlite> SELECT Id, Title, IsDone FROM Todos;
sqlite> .quit
```

This tells you whether a bug is in the data or in your code.

## Debugging tests

In VS Code, open the Testing view from the side bar. Each test has a Debug Test button that runs it with the debugger attached, so breakpoints in the test and in your endpoints both work. In Visual Studio, right-click a test in Test Explorer and choose Debug. The [Testing](/web-api/testing) page shows how to write the tests.

## Learn more

- [Logging](/web-api/logging)
- [Error handling](/web-api/errors)
- [Troubleshooting](/web-api/troubleshooting)
- [Troubleshoot and debug ASP.NET Core projects](https://learn.microsoft.com/aspnet/core/test/troubleshoot)
