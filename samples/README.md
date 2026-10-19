# Sample apps

Each folder is the finished app from Part 1 of its track, built by following the guide page by page.

| Sample | Track | Matches |
|---|---|---|
| `BookShelf` | MVC | End of Part 1 in the MVC sidebar |
| `TodoApi` | Web API (Minimal APIs) | End of Part 1 in the Web API sidebar |

## Run BookShelf

```bash
cd samples/BookShelf
dotnet run
```

The app creates `bookshelf.db`, applies migrations, and adds sample books on start in Development. In Development, emails are written to the log, so you need no SMTP server.

For a real SMTP server, set the password once:

```bash
dotnet user-secrets set "Smtp:Password" "your-password"
```

Run the tests from the `samples` folder:

```bash
dotnet test BookShelf.slnx
```

## Run TodoApi

```bash
cd samples/TodoApi
dotnet run
```

The API listens on http://localhost:5000 and https://localhost:5001. It migrates and seeds on start in Development. Open `/scalar` for the API reference.

Protected endpoints need a token. Create one for local testing:

```bash
dotnet user-jwts create --name alice --role Admin
```

Send it as `Authorization: Bearer <token>`. For a real SMTP server, set `Email:UserName` and `Email:Password` with `dotnet user-secrets set`. smtp4dev needs neither.

```bash
dotnet test TodoApi.slnx
```
