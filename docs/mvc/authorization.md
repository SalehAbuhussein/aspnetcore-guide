# Authorization

## Introduction

Authorization answers the question "is this user allowed to do this?" It runs after [authentication](/mvc/authentication) has worked out who the user is. ASP.NET Core lets you protect controllers and actions with attributes, roles, and policies.

## Requiring a signed-in user

```csharp [Controllers/BooksController.cs (excerpt)]
using Microsoft.AspNetCore.Authorization;

[Authorize]
public class BooksController(AppDbContext db) : Controller
{
    // GET: /Books?search=dune&page=2
    [AllowAnonymous]
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        // The search and paging code from the Querying page stays the same.
    }

    // Details, Create, Edit, and Delete stay as they are.
}
```

An attribute is a tag in square brackets that adds information to a class or method. `[Authorize]` on the controller means every action needs a signed-in user. `[AllowAnonymous]` on `Index` opens that one action to everyone, including visitors who have not logged in. Add the `using` line at the top of the file and the two attributes. The rest of the controller does not change.

When a visitor without an account hits a protected action, ASP.NET Core redirects them to the login page. After they sign in, Identity sends them back to the page they asked for.

## Roles

A role is a named group of users, such as "Admin" or "Editor". Turn on role support where you register Identity.

```csharp [Program.cs (excerpt)]
builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();
```

If you created the project with `--auth Individual`, your context is named `ApplicationDbContext` instead.

Then limit an action to a role.

```csharp [Controllers/BooksController.cs (excerpt)]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> Delete(int id)
{
    var book = await db.Books.FindAsync(id);
    if (book is null) return NotFound();
    return View(book);
}

// POST: /Books/Delete/5
[HttpPost, ActionName("Delete")]
[ValidateAntiForgeryToken]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> DeleteConfirmed(int id)
```

Put the attribute on both `Delete` actions. The GET action only shows the confirm page, while the POST action deletes the book, so a user who skips the page and sends the form directly would still delete books if only the GET action were protected. The body of `DeleteConfirmed` does not change.

Only users in the Admin role can reach `Delete`. A signed-in user without the role gets an "access denied" page instead of the login page. List several roles with commas, as in `Roles = "Admin,Editor"`, to allow users in any of them.

You add users to roles with `UserManager<IdentityUser>`, for example `await userManager.AddToRoleAsync(user, "Admin")`. The role must exist first, which you can create with `RoleManager<IdentityRole>`.

## Policies

A policy is a named rule that you define once in `Program.cs` and reuse by name. Policies keep rules in one place, so you can change who counts as an editor without touching every controller.

```csharp [Program.cs (excerpt)]
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("CanEditBooks", policy => policy.RequireRole("Admin", "Editor"))
    .AddPolicy("HasEmail", policy => policy.RequireClaim(ClaimTypes.Email));
```

`RequireRole` passes when the user is in any of the listed roles. `RequireClaim` passes when the user has a given claim, which is a piece of information about the user such as their email. Add `using System.Security.Claims;` at the top of `Program.cs` for `ClaimTypes`.

Apply a policy with the `Policy` property.

```csharp [Controllers/BooksController.cs (excerpt)]
[Authorize(Policy = "CanEditBooks")]
public async Task<IActionResult> Edit(int id)
{
    var book = await db.Books.FindAsync(id);
    if (book is null) return NotFound();
    return View(book);
}

// POST: /Books/Edit/5
[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Policy = "CanEditBooks")]
public async Task<IActionResult> Edit(int id, Book book)
```

As with `Delete`, protect the POST `Edit` action too. It is the one that saves changes. Its body does not change.

::: tip
Prefer policies over role strings in attributes. A typo in `Roles = "Admn"` fails silently, while a typo in a policy name throws an error the first time the action runs.
:::

## Checking in views

```razor [Views/Books/Index.cshtml (excerpt)]
@foreach (var book in Model.Books)
{
    <tr>
        <td><a asp-action="Details" asp-route-id="@book.Id">@book.Title</a></td>
        <td>@book.Author</td>
        <td>@book.ReviewCount reviews</td>
        @if (User.IsInRole("Admin"))
        {
            <td><a asp-action="Delete" asp-route-id="@book.Id">Delete</a></td>
        }
    </tr>
}
```

`User.IsInRole` returns `true` when the current user is in that role. Use it to hide buttons the user cannot use.

::: warning
Hiding a link in a view does not protect the action. Always put `[Authorize]` on the controller or action as well, because anyone can type a URL by hand.
:::

To check a policy in a view, inject `IAuthorizationService` with `@inject` and call `AuthorizeAsync(User, "CanEditBooks")`. See [Dependency injection](/mvc/dependency-injection) for how `@inject` works.

## Learn more

- [Introduction to authorization in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/authorization/introduction)
- [Policy-based authorization in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/authorization/policies)
