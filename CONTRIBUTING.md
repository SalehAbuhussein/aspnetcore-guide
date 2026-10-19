# Writing pages for this guide

## Page shape (Laravel docs style)
- `# Title`, then `## Introduction` with 1 to 3 sentences on what the page gives you.
- One H2 per subtopic. Command or code first, then 1 to 3 sentences.
- Code fences carry a language and a file label: ```csharp [Controllers/BooksController.cs]. When a block changes part of an existing file, label it `(excerpt)` and never paste a whole file over code from earlier pages.
- At most two `::: tip` / `::: warning` containers per page.
- Internal links are absolute with no `.md`: `[Caching](/mvc/caching)`. Only link pages listed in `docs/.vitepress/config.mts`.
- End with `## Learn more`: pages on this site first, then one learn.microsoft.com page.
- How-to guides (`howto-*`): numbered H2 steps (`## 1. Install the package`), last step `## N. Try it`.

## Projects
- MVC track: `BookShelf`, `Book { Id, Title, Author, Year }`, `Data/AppDbContext`, SQLite, connection string `Default`, `BooksController(AppDbContext db)`.
- Web API track: `TodoApi`, `Todo { Id, Title, IsDone }`, DTOs in `Models/TodoDtos.cs`, endpoints in `Endpoints/TodoEndpoints.cs` (`MapTodoEndpoints` returns void), `builder.Services.AddValidation()`.
- Finished Part 1 apps live in `samples/`. New code must work on top of them.
- Target .NET 10, C# 14, EF Core 10. Every sample must build. Check it before publishing.

## Web API: Minimal APIs / Controllers switch
- Put `<ApiStyle />` once, on its own line, after the Introduction.
- Wrap each part that differs in a pair, with blank lines inside the divs:
  `<div class="only-minimal">` ... `</div>` and `<div class="only-controllers">` ... `</div>`.
- No headings inside a pair. Shared code (models, DTOs, services) stays outside.
- Controllers: `[ApiController] [Route("todos")] public class TodosController(AppDbContext db) : ControllerBase`, `AddControllers()` and `MapControllers()`, attributes such as `[Authorize]`, `[EnableRateLimiting]`, `[OutputCache]`.

## Writing rules
- Second person, active voice, short sentences, plain words. Explain a term once, the first time it appears.
- Sentence case headings. Straight quotes. No emoji.
- No em or en dashes, and no parentheses used as a dash substitute.
- Do not use: delve, crucial, robust, seamless, leverage, utilize, powerful, simply, just, easily, additionally, "in order to", "it's important to note", "not only X but Y", "let's", marketing tone, closing summary paragraphs.
- No bold labels followed by a colon.
