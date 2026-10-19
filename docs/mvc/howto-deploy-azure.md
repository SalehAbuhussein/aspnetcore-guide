# Deploy to Azure

## Introduction

At the end of this guide, BookShelf runs on Azure App Service at `https://<your-app>.azurewebsites.net`, with its connection string stored as an app setting and its database schema applied by a migration bundle. Before you start, you need an Azure subscription, the [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) installed, and a server database. Read [Deployment](/mvc/deployment) first for publishing and migration bundles.

## 1. Pick a database

SQLite stores data in a local file, which does not suit App Service. Files outside `/home` are lost on restart, and the file cannot be shared between several instances. Use a server database instead.

- Azure SQL Database. Install `Microsoft.EntityFrameworkCore.SqlServer` and call `UseSqlServer`.
- Azure Database for PostgreSQL flexible server. Follow [Switch to PostgreSQL](/mvc/howto-postgres) first.

Both work the same way in the steps below. Create the database from the Azure portal or with `az sql server create` or `az postgres flexible-server create`. Allow access from Azure services in its networking settings.

## 2. Sign in and create the resources

```bash
az login

az group create --name bookshelf-rg --location westeurope

az appservice plan create --name bookshelf-plan --resource-group bookshelf-rg \
  --sku B1 --is-linux

az webapp create --name bookshelf-yourname --resource-group bookshelf-rg \
  --plan bookshelf-plan --runtime "DOTNETCORE:10.0"
```

A resource group is a folder for related Azure resources. Delete it and everything inside goes too. The App Service plan is the machine your app runs on. B1 is a small paid tier. F1 is free but sleeps when idle. The web app name must be unique across Azure, because it becomes part of the URL. Run `az webapp list-runtimes --os linux` to see the exact runtime names.

`az webapp up` does all of this in one command, and it is fine for a first try. The separate commands make each resource name explicit, which you want when you deploy again.

## 3. Set the connection string as an app setting

```bash
az webapp config appsettings set --name bookshelf-yourname --resource-group bookshelf-rg \
  --settings "ConnectionStrings__Default=Host=yourserver.postgres.database.azure.com;Database=bookshelf;Username=bookshelf;Password=<password>;Ssl Mode=Require"
```

App Service passes app settings to your app as environment variables. `ConnectionStrings__Default` overrides `ConnectionStrings:Default` from `appsettings.json`, the same rule as in [Configuration](/mvc/configuration). The password stays out of your code and your repository. Add other secrets, such as `Smtp__Password`, the same way.

## 4. Publish and deploy a zip

```bash
dotnet publish -c Release -o publish
cd publish
zip -r ../app.zip .
cd ..

az webapp deploy --name bookshelf-yourname --resource-group bookshelf-rg \
  --src-path app.zip --type zip
```

`dotnet publish` builds the app into the `publish` folder. The zip must contain the files themselves, not the folder, so it is created from inside the folder. On PowerShell, use `Compress-Archive -Path publish\* -DestinationPath app.zip`.

## 5. Apply migrations with a bundle

```bash
dotnet ef migrations bundle -o efbundle --self-contained -r linux-x64
./efbundle --connection "<the same connection string as step 3>"
```

The bundle is one program that applies any pending [migrations](/mvc/migrations). Run it from your machine or your build pipeline before or right after the deploy, against the Azure database. `-r linux-x64` matches a Linux runner. Use `-r win-x64 -o efbundle.exe` to run it on Windows. Your machine's IP address must be allowed in the database firewall.

The `db.Database.Migrate()` startup option from [Deployment](/mvc/deployment) also works on a single instance, but stops working safely once you scale out.

## 6. Read the logs

```bash
az webapp log config --name bookshelf-yourname --resource-group bookshelf-rg \
  --docker-container-logging filesystem

az webapp log tail --name bookshelf-yourname --resource-group bookshelf-rg
```

The first command turns on logging of console output. The second streams it to your terminal, including startup errors. `Ctrl+C` stops the stream.

## 7. Try it

Open `https://bookshelf-yourname.azurewebsites.net/Books`. You see the books list, served over HTTPS with Azure's certificate. Add a book, then reload the page to check it was saved in the database.

If you get "HTTP Error 500.30" or a blank error page, run `az webapp log tail`. The usual causes are a missing app setting or a database firewall that blocks App Service.

When you are done testing, remove everything.

```bash
az group delete --name bookshelf-rg
```

## Learn more

- [Deployment](/mvc/deployment)
- [Configuration](/mvc/configuration)
- [Switch to PostgreSQL](/mvc/howto-postgres)
- [Deploy an ASP.NET Core app to Azure App Service](https://learn.microsoft.com/azure/app-service/quickstart-dotnetcore)
