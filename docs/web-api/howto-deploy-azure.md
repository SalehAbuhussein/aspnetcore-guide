# Deploy to Azure

## Introduction

At the end of this guide `TodoApi` runs on Azure App Service on Linux, talks to an Azure Database for PostgreSQL server, and streams its logs to your terminal. Every step uses the `az` command line, so you can paste them into a script later.

Before you start, you need `TodoApi` on PostgreSQL from [Switch to PostgreSQL](/web-api/howto-postgres), an Azure subscription, and the Azure CLI signed in with `az login`. [Deployment](/web-api/deployment) explains publishing and migration bundles in more depth.

## 1. Pick names

```bash
RG=todoapi-rg
LOCATION=westeurope
APP=todoapi-$RANDOM
DB=todoapi-db-$RANDOM
DB_PASSWORD='choose-a-long-password'
```

App and database server names must be unique across Azure, because they become part of a public host name. `$RANDOM` adds a number to make that likely.

## 2. Create the database

```bash
az group create --name $RG --location $LOCATION

az postgres flexible-server create \
  --resource-group $RG --name $DB --location $LOCATION \
  --admin-user todos --admin-password "$DB_PASSWORD" \
  --tier Burstable --sku-name Standard_B1ms \
  --public-access 0.0.0.0

az postgres flexible-server db create \
  --resource-group $RG --server-name $DB --database-name todos
```

A resource group is a folder for everything this app uses, so you can delete it all in one go. `--public-access 0.0.0.0` lets other Azure services, including your app, reach the server. Add your own IP address with `az postgres flexible-server firewall-rule create` to run migrations from your machine.

## 3. Create the App Service

```bash
az appservice plan create --resource-group $RG --name todoapi-plan --is-linux --sku B1

az webapp create --resource-group $RG --plan todoapi-plan --name $APP \
  --runtime "DOTNETCORE:10.0"
```

The plan is the machine your app runs on, and `B1` is a small paid tier. Run `az webapp list-runtimes --os linux` to see the exact runtime names Azure offers today.

## 4. Set the settings

```bash
az webapp config appsettings set --resource-group $RG --name $APP --settings \
  ASPNETCORE_ENVIRONMENT=Production \
  "ConnectionStrings__Default=Host=$DB.postgres.database.azure.com;Database=todos;Username=todos;Password=$DB_PASSWORD;Ssl Mode=Require"
```

App settings reach your app as environment variables, so `ConnectionStrings__Default` overrides the value in `appsettings.json`, the same as on [Deployment](/web-api/deployment). Azure encrypts them at rest. Add your other secrets, such as `Email__Password` or `Admin__Password`, the same way. For many secrets, move them to Azure Key Vault.

## 5. Apply the migrations

```bash
dotnet ef migrations bundle --self-contained -r linux-x64 -o efbundle
./efbundle --connection "Host=$DB.postgres.database.azure.com;Database=todos;Username=todos;Password=$DB_PASSWORD;Ssl Mode=Require"
```

A migration bundle is one executable that applies your migrations. Run it from a machine whose IP the firewall allows, or as a step in your [CI/CD](/web-api/ci-cd) pipeline, before the new code goes live. Build it for the machine that runs it: use `-r win-x64` if you run it on Windows.

## 6. Publish and deploy

```bash
dotnet publish -c Release -o ./publish
cd publish && zip -r ../app.zip . && cd ..

az webapp deploy --resource-group $RG --name $APP --src-path app.zip --type zip
```

`az webapp deploy` uploads the zip and restarts the app. On Windows without `zip`, use `Compress-Archive -Path publish/* -DestinationPath app.zip` in PowerShell.

## 7. Try it

```bash
az webapp log config --resource-group $RG --name $APP --docker-container-logging filesystem
az webapp log tail --resource-group $RG --name $APP
```

Leave the log tail running in one terminal. In another, call the app:

```bash
curl -i https://$APP.azurewebsites.net/health
```

You get `200 OK` with `Healthy` if you added the endpoint from [Health checks](/web-api/health-checks), and the request shows in the log tail. Then try `POST /todos` or `POST /register` against the same host. A `500` usually means a wrong connection string. The log tail shows the exception.

When you are done experimenting, delete everything:

```bash
az group delete --name $RG --yes
```

::: warning
These commands are documented, not run as part of this site's checks. Azure changes SKU names and runtime lists over time, so check `az <command> --help` if a flag is refused.
:::

## Learn more

- [Deployment](/web-api/deployment)
- [CI/CD](/web-api/ci-cd)
- [Health checks](/web-api/health-checks)
- [Deploy ASP.NET Core apps to Azure App Service](https://learn.microsoft.com/aspnet/core/host-and-deploy/azure-apps/)
