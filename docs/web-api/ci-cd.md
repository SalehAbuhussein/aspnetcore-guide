# CI/CD

## Introduction

Building and testing by hand works until someone forgets to run the tests before a merge. Continuous integration (CI) builds and tests every push automatically, and continuous delivery (CD) packages the result so it is ready to deploy. You want this as soon as more than one person pushes to the repository, or as soon as you deploy more than once a month.

This page uses GitHub Actions and assumes the layout from [Integration tests](/web-api/testing): a `TodoApi.slnx` file at the repository root with `TodoApi/` and `TodoApi.Tests/` next to it.

## Build and test on every push

```yaml [.github/workflows/ci.yml]
name: CI

on:
  push:
    branches: [main]
  pull_request:

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore -c Release

      - name: Test
        run: dotnet test --no-build -c Release

      - name: Publish
        run: dotnet publish TodoApi --no-build -c Release -o out

      - uses: actions/upload-artifact@v4
        with:
          name: todoapi
          path: out
```

A workflow is a YAML file in `.github/workflows`. GitHub runs it on every push to `main` and every pull request. `setup-dotnet` installs the latest .NET 10 SDK, and `10.0.x` picks up patch releases automatically.

`--no-restore` and `--no-build` reuse the output of the step before, so each step only does its own job. If a test fails, the job stops and the pull request shows a red check. `upload-artifact` saves the published files so a later job or a person can download them.

## Building a container image

The .NET SDK can build and push an image without a Dockerfile, as shown on the [Deployment](/web-api/deployment) page. Add a second job that pushes to GitHub Container Registry (GHCR) after the tests pass on `main`.

```yaml [.github/workflows/ci.yml (excerpt)]
  container:
    needs: build
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    permissions:
      contents: read
      packages: write
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x

      - uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Publish image
        run: >
          dotnet publish TodoApi -c Release /t:PublishContainer
          -p ContainerRegistry=ghcr.io
          -p ContainerRepository=your-name/todoapi
          -p ContainerImageTags='"${{ github.sha }};latest"'
```

`needs: build` makes this job wait for the tests. The `packages: write` permission lets the built-in `GITHUB_TOKEN` push to GHCR, and `docker/login-action` stores that login where the SDK can find it. Replace `your-name` with your GitHub user or organization in lowercase, because image names must be lowercase.

Tagging with the commit SHA gives every build a unique tag you can deploy and roll back to. `latest` always points at the newest build from `main`.

If you already have a Dockerfile, `docker/build-push-action@v6` with `push: true` and the same login step does the same job. Pick one approach and stay with it.

## Bundling EF Core migrations

```yaml [.github/workflows/ci.yml (excerpt)]
      - name: Install EF Core tools
        run: dotnet tool install --global dotnet-ef

      - name: Build migrations bundle
        run: dotnet ef migrations bundle --project TodoApi --configuration Release -r linux-x64 --self-contained -o efbundle

      - uses: actions/upload-artifact@v4
        with:
          name: efbundle
          path: efbundle
```

Add these steps to the `build` job. A migrations bundle is a single executable that applies your [migrations](/web-api/migrations) to a database. It needs no .NET SDK and no source code on the machine that runs it, which makes it a good fit for a deploy step.

```bash
./efbundle --connection "$DB_CONNECTION"
```

Run it in your deploy job or release pipeline before the new version of the app starts. `DB_CONNECTION` holds the connection string, set from a pipeline secret, never from the repository. The bundle applies only the migrations the database does not have yet, so running it twice is safe.

::: warning
Do not call `Database.Migrate()` at app startup in production. With several instances starting at once, they race to apply the same migration. A separate bundle step runs exactly once per deployment.
:::

::: tip
Pin the `dotnet-ef` version with a local tool manifest (`dotnet new tool-manifest`, then `dotnet tool install dotnet-ef`) and run `dotnet tool restore` in CI. Then CI uses the same tool version as your machine.
:::

## Learn more

- [Containerize a .NET app with dotnet publish](https://learn.microsoft.com/dotnet/core/containers/sdk-publish)
- [Applying migrations](https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying)
