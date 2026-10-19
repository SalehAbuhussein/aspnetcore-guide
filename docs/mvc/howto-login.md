# Add login

## Introduction

At the end of this guide, BookShelf has Register, Log in, and Log out links, and the books pages ask visitors to sign in before they can change anything. Before you start, you need the BookShelf app with `AppDbContext` from [Database](/mvc/database) and the `dotnet ef` tool from [Migrations](/mvc/migrations). This is the short path. [Authentication](/mvc/authentication) explains each piece in more detail.

## 1. Install the packages

```bash
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add package Microsoft.AspNetCore.Identity.UI
```

The first package stores users with EF Core. The second contains the ready-made Register, Login, and account pages.

## 2. Make the context an Identity context

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Your HasData and relationship code from earlier pages stays here.
    }
}
```

`IdentityDbContext<IdentityUser>` adds the tables for users, roles, and logins. Keep your other `DbSet` properties. Call `base.OnModelCreating` first so Identity can configure its tables.

## 3. Register Identity

```csharp [Program.cs (excerpt)]
using Microsoft.AspNetCore.Identity;

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<AppDbContext>();

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
```

`AddDefaultIdentity` registers Identity, the sign-in cookie, and the default account pages. `MapRazorPages` makes those pages reachable under `/Identity/Account/...`. Without it, the Login link returns 404.

`RequireConfirmedAccount = true` makes new users confirm their email before they can log in. In development, the page shown after registering has a link you can click to confirm.

## 4. Add the login links

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

Render it in the navigation bar of `Views/Shared/_Layout.cshtml`.

```razor [Views/Shared/_Layout.cshtml (excerpt)]
<partial name="_LoginPartial" />
```

The Identity pages need this file. If it is missing, they fail with "The default Identity UI layout requires a partial view '_LoginPartial'".

## 5. Create the user tables

```bash
dotnet ef migrations add AddIdentity
dotnet ef database update
```

The migration adds tables such as `AspNetUsers` and `AspNetRoles` next to your `Books` table.

## 6. Protect the books pages

```csharp [Controllers/BooksController.cs (excerpt)]
using Microsoft.AspNetCore.Authorization;

[Authorize]
public class BooksController(AppDbContext db) : Controller
{
    [AllowAnonymous]
    public async Task<IActionResult> Index() => View(await db.Books.ToListAsync());

    // other actions as before
}
```

`[Authorize]` on the class means every action needs a signed-in user. `[AllowAnonymous]` keeps the list open to everyone. [Authorization](/mvc/authorization) covers roles and policies.

## 7. Try it

Run `dotnet run` and open `https://localhost:<port>/Books/Create`. You are sent to `/Identity/Account/Login?ReturnUrl=%2FBooks%2FCreate`. Click Register, create an account, click the confirmation link on the next page, then log in. You land back on the Create form, and the top of the page reads "Hello, you@example.com".

You can check the redirect from the command line too.

```bash
curl -k -i https://localhost:<port>/Books/Create
```

The response is `302 Found` with a `Location` header that points to the login page.

## Learn more

- [Authentication](/mvc/authentication)
- [Authorization](/mvc/authorization)
- [Reset a forgotten password](/mvc/howto-password-reset)
- [Introduction to Identity on ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/authentication/identity)
