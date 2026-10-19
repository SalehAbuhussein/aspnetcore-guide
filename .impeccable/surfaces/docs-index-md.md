---
version: 1
slug: "docs-index-md"
primary_target: "docs/index.md"
related_targets: ["docs/.vitepress/theme/custom.css"]
---

Scope: the whole docs site (home + MVC track + Web API track). Mode: Read.
Audience: programmers new to ASP.NET Core, reading beside an editor and terminal.
Job: pick a track, follow pages in order, end with a working .NET 10 app.
Constraint: look and structure pinned by the user to the Laravel docs.

## Direction contract
THESIS: Laravel-docs reading experience for ASP.NET Core, split into two self-contained tracks. Refuses the sprawling Microsoft Learn tree where MVC, Razor Pages, Blazor and APIs interleave.
OWN-WORLD: White page (deep violet-black in dark), .NET purple #512BD4 as the only accent, Figtree text, JetBrains Mono code, bordered rounded code blocks, bold sidebar group titles with muted items and purple active item, no H2 rules.
STORY: Reader understands MVC means HTML from the server and Web API means JSON, picks one, and follows numbered-in-order pages without meeting the other model.
FIRST VIEWPORT: Home: large H1 left, one-line lede, then two equal track panels side by side, each showing the code it writes and what the client receives, footer link row as the action. Docs pages: Laravel three-column, sidebar with MVC/Web API switch on top, content, "On this page" outline.
FORM: Pinned by brief (Laravel docs). Concept roll skipped because a brief-pinned direction beats the roll. Seed key: none (pinned). Signature interaction: track switch keeps you on the same topic in the other track.
FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance
