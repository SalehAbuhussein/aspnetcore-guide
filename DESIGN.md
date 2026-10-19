---
name: ASP.NET Core Guide
description: Laravel-style docs for ASP.NET Core, split into MVC and Web API tracks.
colors:
  dotnet-purple: "#512bd4"
  dotnet-purple-deep: "#3f1fb0"
  dotnet-purple-night: "#a593ff"
  paper: "#ffffff"
  paper-tint: "#f8f7fb"
  paper-soft: "#f3f1f9"
  hairline: "#e8e5f0"
  ink: "#1b1830"
  ink-muted: "#55526a"
  ink-faint: "#6f6c85"
  night: "#121019"
  night-raised: "#17141f"
  night-hairline: "#2a2636"
  night-ink: "#ecebf3"
typography:
  display:
    fontFamily: "Figtree, ui-sans-serif, system-ui, sans-serif"
    fontSize: "clamp(2.25rem, 5vw, 3.5rem)"
    fontWeight: 800
    lineHeight: 1.05
    letterSpacing: "-0.035em"
  headline:
    fontFamily: "Figtree, ui-sans-serif, system-ui, sans-serif"
    fontSize: "2.25rem"
    fontWeight: 750
    lineHeight: 1.15
    letterSpacing: "-0.025em"
  title:
    fontFamily: "Figtree, ui-sans-serif, system-ui, sans-serif"
    fontSize: "1.5rem"
    fontWeight: 700
    letterSpacing: "-0.015em"
  body:
    fontFamily: "Figtree, ui-sans-serif, system-ui, sans-serif"
    fontSize: "16px"
    fontWeight: 400
    lineHeight: 1.75
  code:
    fontFamily: "JetBrains Mono, ui-monospace, Cascadia Code, monospace"
    fontSize: "0.84em"
    fontWeight: 400
rounded:
  sm: "5px"
  md: "10px"
  lg: "14px"
spacing:
  gutter-mobile: "16px"
  panel: "28px"
  section: "72px"
components:
  track-switch-active:
    backgroundColor: "{colors.paper}"
    textColor: "{colors.dotnet-purple}"
    rounded: "{rounded.md}"
    padding: "7px 0"
  track-panel:
    backgroundColor: "{colors.paper}"
    rounded: "{rounded.lg}"
  code-block:
    backgroundColor: "{colors.paper-tint}"
    rounded: "{rounded.md}"
  inline-code:
    textColor: "{colors.dotnet-purple-deep}"
    rounded: "{rounded.sm}"
---

# Design System: ASP.NET Core Guide

## Overview

**Creative North Star: "The Laravel handbook, in .NET purple"**

The user pinned the Laravel docs as the reference, so the layout copies them: a bold-titled sidebar on the left, one readable column in the middle, "On this page" on the right. The only change is the accent. Laravel's red becomes .NET purple, and it stays the one color on the page.

The page is quiet so the code is easy to read. Code blocks carry the visual weight. Headings are large and tight, with no rule lines between sections.

**Key characteristics:**
- One accent, purple, used for links, the active page, and the selected track
- Three columns on desktop, VitePress's drawer on mobile
- A track switch where Laravel puts its version picker
- Light and dark modes with the same layout

## Colors

Neutrals carry a slight violet tint so they sit well with the purple.

### Primary
- **.NET Purple** (#512bd4): links, active sidebar item, active track, inline code tint, focus ring, selection. In dark mode it lifts to **Night Purple** (#a593ff) to keep contrast.

### Neutral
- **Paper** (#ffffff) and **Night** (#121019): page ground.
- **Paper Tint** (#f8f7fb) and **Night Raised** (#17141f): code panels, the track switch tray.
- **Hairline** (#e8e5f0) and **Night Hairline** (#2a2636): every border and divider.
- **Ink** (#1b1830), **Ink Muted** (#55526a), **Ink Faint** (#6f6c85): headings, body, captions.

### Named rules
**The One Accent Rule.** Purple is the only hue outside syntax highlighting. Do not add a second accent per track.

## Typography

**Body and display:** Figtree. **Code:** JetBrains Mono.

Figtree is the face Laravel's docs used, which ties the site to its reference. Weight does the hierarchy work: 800 for the home title, 750 for page titles, 700 for sections, 400 for body.

### Hierarchy
- **Display** (800, clamp 2.25 to 3.5rem, 1.05): home page title only.
- **Headline** (750, 2.25rem, 1.15): page H1.
- **Title** (700, 1.5rem): H2 sections, no top rule.
- **Body** (400, 16px, 1.75): prose and lists, VitePress's 688px column.
- **Code** (JetBrains Mono, 0.84em): blocks and inline code.

## Layout

Desktop uses VitePress's three-column doc layout. The home page is a 1080px column: title, then two equal track panels in a `minmax(0, 1fr)` grid, then a "Before you start" row. Below 768px everything stacks in one column with a 16px gutter. Sections sit about 72px apart, with more space above a heading than below it.

## Elevation & Depth

The pages are flat, with layers set apart by tint and hairlines. Shadows appear in two places only. The active track segment gets `0 1px 2px rgba(27,24,48,.08), 0 2px 6px rgba(27,24,48,.06)`. A hovered track panel gets `0 8px 30px rgba(27,24,48,.08)`.

## Shapes

Code blocks and the track switch use a 10px radius, inline code 5px, and home panels 14px. Every border is a 1px hairline. Nothing uses a colored side stripe.

## Components

- **Track switch:** a two-segment control at the top of the sidebar. The active segment is a raised white chip with purple text. Switching keeps you on the same topic when the other track has a page with that name, otherwise it goes to that track's introduction.
- **Track panel (home):** a heading and one line of text, then the code the track writes and what the client receives, then a full-width footer link with an arrow that moves 4px on hover.
- **Code style switch (Web API pages):** "Show code as: Minimal APIs | Controllers", placed once under the Introduction. Same segmented look as the track switch. The choice is global and saved in localStorage, and a head script applies it before first paint. Content pairs use `<div class="only-minimal">` and `<div class="only-controllers">`; headings never go inside a pair, so "On this page" stays the same for both styles.
- **Code block:** a tinted panel with a hairline border and 10px radius. On the home page the language label is hidden.

## Do's and Don'ts

- Do keep pages in the Laravel shape: `# Title`, `## Introduction`, one topic per H2, code first.
- Do keep purple as the only accent.
- Don't add eyebrow labels, gradient text, or icon-card grids.
- Don't put a code block inside a link. Copy buttons would sit inside the link.
