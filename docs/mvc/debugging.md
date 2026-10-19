# Debugging

## Introduction

A debugger lets you pause the app on any line, look at every variable, and step through the code one line at a time. This page shows how to do that in VS Code and Visual Studio, how to read the error pages and stack traces ASP.NET Core gives you, and how to look inside the BookShelf database.

## Start the debugger in VS Code

Install the C# Dev Kit extension, then open the BookShelf folder. Open `Program.cs` and press F5. The first time, VS Code asks which debugger to use. Pick C#, then pick the `http` launch profile. The app builds, starts, and opens in the browser.

You can also open the Run and Debug view from the side bar and click the green Run button. VS Code saves your choice in `.vscode/launch.json`, so the next F5 starts straight away. Press Shift+F5 to stop.

## Breakpoints

Click in the gutter to the left of a line number in `Controllers/BooksController.cs`. A red dot appears. That is a breakpoint. Load `/Books` in the browser and VS Code pauses on that line before it runs.

While paused, you can:

- Hover over a variable to see its value. The Variables panel lists every local variable.
- Press F10 to step over the current line, F11 to step into a method call, and F5 to continue.
- Add an expression such as `book.Title` to the Watch panel. It updates after every step.
- Read the Call Stack panel. It lists the chain of methods that led to this line, newest at the top. Click a row to jump to that method.

Right-click a breakpoint and pick Edit Breakpoint to make it conditional. A condition such as `id == 5` pauses only when it is true, which saves you from clicking Continue fifty times in a loop.

## Visual Studio

Open `BookShelf.slnx` and press F5. Breakpoints, stepping keys, Watch, and Call Stack work the same way as in VS Code. Right-click a breakpoint and choose Conditions for a conditional breakpoint.

## dotnet watch or the debugger

`dotnet watch` rebuilds and reloads the app each time you save a file. Use it while you write views and CSS, because you see changes in seconds. It does not pause on breakpoints. Start the debugger when you need to know why something happens, such as a value that is `null` when it should not be. You cannot run both on the same port at once, so stop one before you start the other.

## The developer exception page

When your code throws an exception in Development, ASP.NET Core shows the developer exception page instead of a blank error. It shows the exception type, the message, and the stack trace, with tabs for the query string, cookies, headers, and routing. Outside Development, users see the friendly error page from [Error handling](/mvc/errors) instead, because the details can leak secrets.

::: warning
Never turn the developer exception page on in Production. It shows source file paths and request data to anyone.
:::

## Reading a stack trace

```text
System.InvalidOperationException: Sequence contains no elements.
   at System.Linq.ThrowHelper.ThrowNoElementsException()
   at System.Linq.Enumerable.First[TSource](IEnumerable`1 source)
   at BookShelf.Controllers.BooksController.Details(Int32 id) in C:\src\BookShelf\Controllers\BooksController.cs:line 42
   at lambda_method12(Closure, Object, Object[])
   at Microsoft.AspNetCore.Mvc.Infrastructure.ActionMethodExecutor...
```

The first line names the exception type and its message. Each line after it is one method call, with the method that threw at the top. Skip the `System` and `Microsoft` lines and find the first line from your own code, here `BooksController.Details` at line 42. Put a breakpoint there and run the request again.

## Logging as debugging

Sometimes pausing the app is not practical, for example when a bug only shows up on a server. Write log messages with `ILogger<T>` instead and read them in the terminal. Raise a category to `Debug` in `appsettings.Development.json` to see more detail. The [Logging](/mvc/logging) page covers levels and structured messages.

## Look inside the database

Once you add a database on the [Database](/mvc/database) page, BookShelf stores its data in `bookshelf.db` in the project folder. In VS Code, install a SQLite viewer extension such as SQLite Viewer, then click the file to browse tables. From a terminal, use the `sqlite3` tool:

```bash
sqlite3 bookshelf.db
sqlite> .tables
sqlite> SELECT Id, Title, Year FROM Books;
sqlite> .quit
```

This tells you whether a bug is in the data or in your code.

## Debugging tests

In VS Code, open the Testing view from the side bar. Each test has a Debug Test button that runs it with the debugger attached, so breakpoints in the test and in your controllers both work. In Visual Studio, right-click a test in Test Explorer and choose Debug. The [Testing](/mvc/testing) page shows how to write the tests.

## Learn more

- [Logging](/mvc/logging)
- [Error handling](/mvc/errors)
- [Troubleshooting](/mvc/troubleshooting)
- [Troubleshoot and debug ASP.NET Core projects](https://learn.microsoft.com/aspnet/core/test/troubleshoot)
