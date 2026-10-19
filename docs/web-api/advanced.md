# Advanced

## Introduction

Part 1 gave you everything you need to build and ship `TodoApi`. The pages in Part 2 make that same API faster, harder to break, and easier to change as it grows. None of them are required on day one. Read a page when you hit the problem it solves.

## Foundations from Part 1

Part 2 builds on the Architecture concepts pages near the start of Part 1: [Request lifecycle](/web-api/lifecycle), [Dependency injection](/web-api/dependency-injection), and [Middleware](/web-api/middleware). Every page here assumes you know where your code runs in a request, which those pages explain.

## What is in Part 2

| Group | Pages | What you get |
| --- | --- | --- |
| Architecture | [Organizing a larger app](/web-api/organizing) | How to lay out code once there are many features. |
| Performance | [Caching](/web-api/caching), [Async and cancellation](/web-api/async), [EF Core performance](/web-api/ef-performance) | Fewer database round trips, cheaper queries, and work that stops when the client leaves. |
| Background work | [Queues and retries](/web-api/queues) | Jobs that survive restarts and retry on failure. |
| Integrations | [Calling other APIs](/web-api/http-clients), [Real-time with SignalR](/web-api/real-time) | Safe outgoing HTTP calls and pushing updates to clients. |
| Data | [Transactions and concurrency](/web-api/transactions) | Keeping data correct when several writes or several users collide. |
| API extras | [Versioning](/web-api/versioning) | Changing your API without breaking existing clients. |
| Production | [Health checks](/web-api/health-checks), [Observability](/web-api/observability), [Secrets and hardening](/web-api/security-hardening), [CI/CD](/web-api/ci-cd) | Running the API on a server and knowing when something is wrong. |
| Testing deeper | [Unit tests and mocking](/web-api/unit-testing), [Testing with real databases](/web-api/testcontainers) | Tests that catch real bugs without being slow or brittle. |

## Suggested order

1. [Organizing a larger app](/web-api/organizing). Settle the folder layout before the app gets big.
2. [Caching](/web-api/caching). The cheapest speed win for read-heavy endpoints.
3. [EF Core performance](/web-api/ef-performance). Most slow APIs are slow because of their queries.
4. The rest as you need them. Each page stands on its own.

All examples keep building on the `TodoApi` project from Part 1, including the `Project` entity from [Relationships](/web-api/relationships).

## Learn more

- [ASP.NET Core fundamentals overview](https://learn.microsoft.com/aspnet/core/fundamentals/)
