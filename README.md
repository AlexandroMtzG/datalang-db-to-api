# DataLang - Database to API

Ever wanted to expose your database as an API?

![Database to API](https://yahooder.sirv.com/datalang/seo/database-to-api.png)

## What is Database to API?

This is a .NET 8 app that exposes your database SQL statements as API endpoints.

## Getting Started

💿 Set your `ApiKey` in `appsettings.json` (don't share it).

💿 Run the app.

```
dotnet run --project DataLangServer
```

💿 Test the API.

Either go to http://localhost:5253/swagger or call the API directly:

```
curl  -X POST \
  'http://localhost:5253/api/test' \
  --header 'Accept: */*' \
  --header 'X-Api-Key: YOUR_API_HERE' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "sql": "YOUR_SQL_HERE",
  "connectionType": "odbc_or_postgres",
  "connectionString": "YOUR_CONNECTION_STRING_HERE"
}'
```

💿 Deploy the app.

```
dotnet publish -c Release
```

## What is DataLang?

[DataLang](https://datalang.io) is an AI-powered app that lets you chat with your databases.
