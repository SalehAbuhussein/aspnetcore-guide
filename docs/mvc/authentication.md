# Authentication

## Introduction

Authentication answers the question "who is this user?" ASP.NET Core Identity is the built-in system for user accounts, passwords, and sign-in. This page creates BookShelf with Identity already set up and shows how sign-in works.

## Creating a project with Identity

```bash
dotnet new mvc -n BookShelf --auth Individual
cd BookShelf
dotnet ef database update
```

The `--auth Individual` option tells the template to add Identity with accounts stored in your own database. From the command line, the template uses SQLite and a file named `app.db`. The last command applies the migration that creates the user tables. See [Migrations](/mvc/migrations) if `dotnet ef` is not installed.

Run `dotnet run` and you will see Register and Login links in the top right of every page.

## What the template gives you

The template adds a few pieces on top of a normal MVC project:

- `Data/ApplicationDbContext.cs`, a context that inherits from `IdentityDbContext` and includes tables for users, roles, and logins.
- A migration in `Data/Migrations` that creates those tables.
- Registration, login, logout, and account pages from the Identity UI package, served under `/Identity/Account/...`.
- `Views/Shared/_LoginPartial.cshtml`, which shows the sign-in links in the layout.

The account pages are Razor Pages, a page-based alternative to controllers. They live inside the Identity UI package in an area, which is a folder that groups related pages under its own URL prefix. You do not see their files in your project, but they work as soon as the app runs.

### Adding Identity to an existing app

If you started BookShelf without Identity, add it in these steps. Run the commands from the `BookShelf` folder.

1. Add the Identity packages.

   ```bash
   dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
   dotnet add package Microsoft.AspNetCore.Identity.UI
   ```

2. Make `AppDbContext` inherit from `IdentityDbContext<IdentityUser>` instead of `DbContext`, and call `base.OnModelCreating` first so Identity can set up its own tables.

   ```csharp [Data/AppDbContext.cs]
   using BookShelf.Models;
   using Microsoft.AspNetCore.Identity;
   using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
   using Microsoft.EntityFrameworkCore;

   namespace BookShelf.Data;

   public class AppDbContext(DbContextOptions<AppDbContext> options)
       : IdentityDbContext<IdentityUser>(options)
   {
       public DbSet<Book> Books => Set<Book>();
       public DbSet<Review> Reviews => Set<Review>();
       public DbSet<Tag> Tags => Set<Tag>();

       protected override void OnModelCreating(ModelBuilder modelBuilder)
       {
           base.OnModelCreating(modelBuilder);

           // Your HasData and relationship code from earlier pages stays here.
       }
   }
   ```

3. Add the Identity lines from the next section to `Program.cs`. Use `AppDbContext` wherever the template code says `ApplicationDbContext`, and use your `Default` connection string wherever it says `DefaultConnection`.

4. Create and apply a migration for the new user tables.

   ```bash
   dotnet ef migrations add AddIdentity
   dotnet ef database update
   ```

The template's `ApplicationDbContext` does the same job as your `AppDbContext`.

## How sign-in works

When a user logs in, Identity checks the password and then sends the browser a cookie. A cookie is a small piece of data that the browser stores and sends back with every request to your site. The authentication cookie is encrypted and holds the user's id and name.

On each later request, ASP.NET Core reads the cookie and rebuilds the user. That is how the app remembers who you are without asking for a password on every page. Logging out deletes the cookie.

## Registering Identity in Program.cs

The Identity lines in `Program.cs` look like this. The block is an excerpt. The template's file also calls `AddDatabaseDeveloperPageExceptionFilter()` and `UseMigrationsEndPoint()`, so do not replace your whole file with it. Add the lines you are missing.

```csharp [Program.cs (excerpt)]
using BookShelf.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
app.MapRazorPages()
    .WithStaticAssets();

app.Run();
```

`AddDefaultIdentity` registers Identity, the cookie handler, and the default UI. `AddEntityFrameworkStores` tells Identity to save users through EF Core. `MapRazorPages` turns on the Identity account pages. The template names its connection string `DefaultConnection`.

`UseAuthentication` reads the cookie and sets the current user. `UseAuthorization` then decides whether that user may see the page. Authentication must come first, because authorization needs to know who the user is. Both go after `UseRouting`. The [Middleware](/mvc/middleware) page explains why order matters.

::: tip
The template omits `UseAuthentication`, because ASP.NET Core adds it for you when authentication services are registered. Writing it out does no harm and makes the order clear.
:::

`RequireConfirmedAccount = true` means a new user must confirm their email before logging in. During development, the register page shows a link you can click to confirm.

## Showing the current user in views

```razor [Views/Shared/_LoginPartial.cshtml]
@if (User.Identity?.IsAuthenticated == true)
{
    <span>Hello, @User.Identity.Name</span>
    <form asp-area="Identity" asp-page="/Account/Logout" method="post">
        <button type="submit">Log out</button>
    </form>
}
else
{
    <a asp-area="Identity" asp-page="/Account/Register">Register</a>
    <a asp-area="Identity" asp-page="/Account/Login">Log in</a>
}
```

Every view and controller has a `User` property for the current user. `User.Identity.Name` holds the user name, which is the email address by default. Logout is a POST form, not a link, so another site cannot log your users out with a hidden image.

If you added Identity to an existing app, create this file yourself and render it in the navigation of `Views/Shared/_Layout.cshtml` with `<partial name="_LoginPartial" />`.

`asp-area` and `asp-page` build URLs to Razor Pages inside an area, here `/Identity/Account/Login` and similar.

To limit pages to signed-in users, continue to [Authorization](/mvc/authorization).

## Learn more

- [Introduction to Identity on ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/authentication/identity)
- [Overview of ASP.NET Core authentication](https://learn.microsoft.com/aspnet/core/security/authentication/)
