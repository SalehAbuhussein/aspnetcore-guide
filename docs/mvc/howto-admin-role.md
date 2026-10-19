# Add an admin role

## Introduction

At the end of this guide, BookShelf creates an Admin role and an admin account when it starts, only admins can delete books, and the Delete links are hidden from everyone else. Before you start, you need login from [Add login](/mvc/howto-login). [Authorization](/mvc/authorization) explains roles in more depth.

## 1. Turn on roles

```csharp [Program.cs (excerpt)]
builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();
```

`AddRoles<IdentityRole>` registers `RoleManager<IdentityRole>` and adds role claims to the sign-in cookie. The role tables already exist, because `IdentityDbContext` created them in the `AddIdentity` migration, so no new migration is needed.

## 2. Put the admin account in configuration

```bash
dotnet user-secrets set "Admin:Email" "admin@example.com"
dotnet user-secrets set "Admin:Password" "Choose-a-long-password-1"
```

The password must pass Identity's default rules: at least six characters, with an uppercase letter, a lowercase letter, a digit, and a symbol. In production, set the `Admin__Email` and `Admin__Password` environment variables, then remove the password once the account exists.

## 3. Write the seeder

```csharp [Data/AdminSeeder.cs]
using Microsoft.AspNetCore.Identity;

namespace BookShelf.Data;

public static class AdminSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        if (!await roles.RoleExistsAsync("Admin"))
        {
            await roles.CreateAsync(new IdentityRole("Admin"));
        }

        var email = config["Admin:Email"];
        var password = config["Admin:Password"];
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            return;
        }

        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }

        if (!await users.IsInRoleAsync(user, "Admin"))
        {
            await users.AddToRoleAsync(user, "Admin");
        }
    }
}
```

`RoleManager` and `UserManager` are scoped services, so the seeder creates a scope to get them. Every step checks first, so running it on each start is safe. `EmailConfirmed = true` lets the admin log in even though `RequireConfirmedAccount` is on. A weak password throws with Identity's error messages, so you find out at startup.

## 4. Run it at startup

```csharp [Program.cs (excerpt)]
var app = builder.Build();

await AdminSeeder.SeedAsync(app.Services, app.Configuration);
```

Call it right after `builder.Build()` and before `app.Run()`. The database must already be migrated. See [Seeding](/mvc/seeding) for other ways to load starting data.

## 5. Limit actions to admins

```csharp [Controllers/BooksController.cs (excerpt)]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> Delete(int id)
{
    var book = await db.Books.FindAsync(id);
    if (book is null) return NotFound();
    return View(book);
}
```

Put the same attribute on the POST `DeleteConfirmed` action too. Each action is checked on its own, and the POST is the one that removes the row. A signed-in user who is not an admin gets the Access denied page.

## 6. Hide admin links in views

```razor [Views/Books/Index.cshtml (excerpt)]
@if (User.IsInRole("Admin"))
{
    <a asp-action="Delete" asp-route-id="@book.Id">Delete</a>
}
```

`User.IsInRole` reads the role claim from the cookie. To add an Admin menu entry for everyone in the role, put the same check around a link in `Views/Shared/_Layout.cshtml`.

```razor [Views/Shared/_Layout.cshtml (excerpt)]
@if (User.IsInRole("Admin"))
{
    <li class="nav-item">
        <a class="nav-link text-dark" asp-controller="Books" asp-action="Index">Manage books</a>
    </li>
}
```

Hiding a link is a convenience for the user. The `[Authorize]` attribute is what protects the action.

## 7. Try it

Run the app and log in at `https://localhost:<port>/Identity/Account/Login` as `admin@example.com`. Open `/Books`. Each book has a Delete link, and the navigation shows Manage books.

Log out and log in as a normal user. The Delete links are gone. Type `/Books/Delete/1` into the address bar, and you land on `/Identity/Account/AccessDenied`.

Role changes show up after the next sign-in, because the role is stored in the cookie. If you add an existing user to Admin, they must log out and back in.

## Learn more

- [Authorization](/mvc/authorization)
- [Seeding](/mvc/seeding)
- [Configuration](/mvc/configuration)
- [Role-based authorization in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/authorization/roles)
