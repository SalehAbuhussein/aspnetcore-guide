# Advanced

## Introduction

Part 1 gives you everything you need to build BookShelf and ship it. The pages in Part 2 make that app faster, sturdier, and easier to change as it grows. Read them when you hit the problem they solve, not before.

::: tip How the samples relate to your code
Part 2 samples start from the short `BooksController(IBookService books)` on the [Dependency injection](/mvc/dependency-injection) page, so each example stays small. Your controller from Part 1 does more and takes more constructor parameters. Keep what you have and add the new parameter or method the page shows.
:::

## Foundations from Part 1

Part 2 builds on the Architecture concepts pages near the start of Part 1: [Request lifecycle](/mvc/lifecycle), [Dependency injection](/mvc/dependency-injection), and [Middleware](/mvc/middleware). If a page here mentions filters, service lifetimes, or pipeline order, those pages explain them.

## What is in Part 2

The pages are grouped by the kind of problem they solve.

| Group | What you get |
| --- | --- |
| Architecture | How to [organize a larger app](/mvc/organizing) once the folders get crowded. |
| Performance | [Caching](/mvc/caching), [async and cancellation](/mvc/async), and [EF Core performance](/mvc/ef-performance). |
| Background work | [Queues and retries](/mvc/queues) with jobs that survive a restart. |
| Integrations | [Calling other APIs](/mvc/http-clients) safely and [real-time updates with SignalR](/mvc/real-time). |
| Data | [Transactions and concurrency](/mvc/transactions) for edits that must not clash. |
| Production | [Health checks](/mvc/health-checks), [observability](/mvc/observability), [secrets and hardening](/mvc/security-hardening), and [CI/CD](/mvc/ci-cd). |
| Testing deeper | [Unit tests and mocking](/mvc/unit-testing) and [testing with real databases](/mvc/testcontainers). |

## A suggested order

1. [Organizing a larger app](/mvc/organizing), before the folders get crowded.
2. [Caching](/mvc/caching), the cheapest speed win in most apps.
3. [EF Core performance](/mvc/ef-performance), because the database is usually the slow part.
4. After that, pick whatever your app needs next. An app that sends email in bulk wants [queues](/mvc/queues). An app that pulls book data from elsewhere wants [HTTP clients](/mvc/http-clients).

Each page assumes the BookShelf project as it stands at the end of Part 1, with `Book`, `Review`, `AppDbContext`, and `BookService`.

## Learn more

- [ASP.NET Core fundamentals overview](https://learn.microsoft.com/aspnet/core/fundamentals/)
- [ASP.NET Core best practices](https://learn.microsoft.com/aspnet/core/fundamentals/best-practices)
