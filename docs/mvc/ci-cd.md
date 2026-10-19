# CI/CD

## Introduction

Building and deploying by hand works until someone forgets a step or ships code that fails its tests. CI (continuous integration) builds and tests every push automatically, and CD (continuous delivery) packages and ships the result. Set this up as soon as more than one person works on BookShelf, or as soon as you deploy more than once a week. This page uses GitHub Actions, and the same steps translate to Azure DevOps or GitLab.

## The repository layout

The workflow assumes this layout, with the solution at the root of the repository.

```text
.github/workflows/ci.yml
BookShelf/
BookShelf/Dockerfile
BookShelf.Tests/
BookShelf.slnx
```

The test project comes from the [Testing](/mvc/testing) page, and the Dockerfile from [Deployment](/mvc/deployment).

## Pinning the EF Core tool

The pipeline needs `dotnet ef` to build a migration bundle. Install it as a local tool so CI uses the same version as you.

```bash
dotnet new tool-manifest
dotnet tool install dotnet-ef
```

Run these at the repository root and commit the `dotnet-tools.json` file they create. `dotnet tool restore` then installs the exact version on any machine.

## The build and test job

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
        run: dotnet build -c Release --no-restore

      - name: Test
        run: dotnet test -c Release --no-build

      - name: Check for vulnerable packages
        run: dotnet list package --vulnerable --include-transitive

      - name: Publish
        run: dotnet publish BookShelf/BookShelf.csproj -c Release --no-build -o publish

      - name: Build migrations bundle
        run: |
          dotnet tool restore
          dotnet ef migrations bundle --project BookShelf/BookShelf.csproj --configuration Release -o efbundle

      - uses: actions/upload-artifact@v4
        with:
          name: bookshelf
          path: publish

      - uses: actions/upload-artifact@v4
        with:
          name: efbundle
          path: efbundle
```

A workflow is a YAML file in `.github/workflows`. The `on` block runs it on every push to `main` and on every pull request. A job is a list of steps that run on one fresh virtual machine, here the latest Ubuntu.

`actions/checkout` clones your repository. `actions/setup-dotnet` installs the newest .NET 10 SDK. `--no-restore` and `--no-build` skip work the earlier steps already did. If any test fails, `dotnet test` exits with an error and the job stops, so broken code never reaches the publish step.

`upload-artifact` saves the published app and the bundle as downloadable files attached to the run. Later jobs, or you, can download them.

::: tip
Turn on branch protection for `main` in your repository settings and require the `build` job to pass. Then nobody can merge a pull request with failing tests.
:::

## The Docker job

The second job builds an image and pushes it to GitHub Container Registry (GHCR), which every GitHub repository can use for free. Add it under `jobs:` in the same file.

```yaml [.github/workflows/ci.yml (excerpt)]
  docker:
    needs: build
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    permissions:
      contents: read
      packages: write
    steps:
      - uses: actions/checkout@v4

      - uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - uses: docker/build-push-action@v6
        with:
          context: ./BookShelf
          push: true
          tags: |
            ghcr.io/your-name/bookshelf:latest
            ghcr.io/your-name/bookshelf:${{ github.sha }}
```

`needs: build` waits for the first job and skips this one if it failed. The `if` line limits pushes to `main`, so pull requests get tested but never shipped. `permissions` lets the built-in `GITHUB_TOKEN` write packages, which is all `docker/login-action` needs to sign in to GHCR.

`docker/build-push-action` runs `docker build` with the Dockerfile in `./BookShelf` and pushes the image. Replace `your-name` with your GitHub user or organization in lowercase, because image names must be lowercase. The commit SHA tag lets you roll back to any exact build.

What happens next depends on your host. Azure App Service, Azure Container Apps, and most container platforms can pull the new image when it lands, or you add one more step that calls their CLI.

## Running migrations in the pipeline

The schema must match the code before the new version starts. Run the bundle from the first job against the production database, then deploy.

```yaml [.github/workflows/ci.yml (excerpt)]
  migrate:
    needs: [build, docker]
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    environment: production
    steps:
      - uses: actions/download-artifact@v4
        with:
          name: efbundle

      - name: Apply migrations
        run: |
          chmod +x efbundle
          ./efbundle --connection "${{ secrets.PRODUCTION_DB }}"
```

A migration bundle is a single program that applies your [migrations](/mvc/migrations) without the SDK or source code. `environment: production` ties the job to a GitHub environment, where you store `PRODUCTION_DB` as a secret and can require a manual approval before the job runs.

The bundle skips migrations that are already applied, so running it twice is safe. Write migrations that the old version of the app can survive, such as adding a column before you start using it, because the old version keeps running until the new one starts.

::: warning
This step only works when the runner can reach your database, such as Azure SQL or PostgreSQL with a firewall rule for the runner. A SQLite file on a server's disk cannot be reached from GitHub. For SQLite, copy the bundle to the server and run it there before restarting the app.
:::

## Learn more

- [GitHub Actions and .NET](https://learn.microsoft.com/dotnet/devops/github-actions-overview)
- [Applying EF Core migrations](https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying)
