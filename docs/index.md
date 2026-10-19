---
layout: page
title: ASP.NET Core Guide
---

<div class="home">

# Build web apps with ASP.NET Core

<p class="lede">A plain, step-by-step guide for .NET 10. Pick a track and read the pages in order.</p>

<div class="tracks">

<div class="track">
<div class="head">
<h2>MVC</h2>
<p>Your server builds HTML pages and sends them to the browser.</p>
</div>
<div class="demo vp-doc">

```razor
<ul>
  @foreach (var book in Model)
  {
    <li>@book.Title</li>
  }
</ul>
```

<div class="out">
<div class="label">Browser receives</div>

```html
<ul><li>Dune</li><li>Emma</li></ul>
```

</div>
</div>
<a class="go" href="./mvc/introduction">Start the MVC guide <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"/></svg></a>
</div>

<div class="track">
<div class="head">
<h2>Web API</h2>
<p>Your server sends JSON. A frontend or mobile app draws the screen.</p>
</div>
<div class="demo vp-doc">

```csharp
app.MapGet("/todos", (AppDbContext db) =>
    db.Todos.ToListAsync());
```

<div class="out">
<div class="label">Client receives</div>

```json
[{ "id": 1, "title": "Buy milk", "isDone": false }]
```

</div>
</div>
<a class="go" href="./web-api/introduction">Start the Web API guide <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"/></svg></a>
</div>

</div>

<div class="pick">
<p><strong>Pick MVC</strong> if people open your site in a browser and you want one project that does everything: pages, forms, login.</p>
<p><strong>Pick Web API</strong> if an Angular, React, or mobile app will call your server, or other services need your data.</p>
</div>

<p class="advanced-row">Already shipped an app? The Advanced pages cover caching, background queues, real-time, observability, and CI/CD: <a href="./mvc/advanced">MVC</a> or <a href="./web-api/advanced">Web API</a>.</p>

<div class="before vp-doc">
<div>
<h2>Before you start</h2>
<p>Install the .NET 10 SDK from <a href="https://dotnet.microsoft.com/download">dotnet.microsoft.com/download</a>, then check it in a terminal. Any version starting with 10 is fine.</p>
</div>

```bash
dotnet --version
# 10.0.100
```

</div>

</div>
