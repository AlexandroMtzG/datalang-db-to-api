using System.Data.Common;
using System.Data.Odbc;
using Microsoft.OpenApi.Models;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var apiKey = builder.Configuration["ApiKey"];

// Register IHttpClientFactory
builder.Services.AddHttpClient();

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "DataLang API", Version = "v1" });

    // Define the API key scheme that's in use (e.g., as a header parameter)
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Name = "X-Api-Key",
        Description = "API key needed to access the endpoints."
    });

    // Make sure the API key is applied to all endpoints
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                },
                In = ParameterLocation.Header,
                Name = "X-Api-Key"
            },
            new List<string>()
        }
    });
});
// Additional services can be added here

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

string[] forbiddenCommands =
[
    "delete", "drop", "update", "insert", "alter", "create", "truncate", "grant", "revoke", "replace"
];
app.MapPost("/api/test", async (HttpContext httpContext, TestConnectionRequest queryRequest,
        ILogger<Program> logger) =>
    {
        logger.LogInformation($"[/api/test] {queryRequest}");

        var apiKeyHeader = httpContext.Request.Headers["X-Api-Key"].FirstOrDefault();
        // Validate the API key
        if (string.IsNullOrEmpty(apiKey))
            return Results.BadRequest("Missing X-Api-Key header");
        if (string.IsNullOrEmpty(apiKey) || apiKey == "{CUSTOM_API_KEY}")
            return Results.BadRequest("Set your ApiKey in appsettings.json");
        if (apiKey != apiKeyHeader)
            return Results.BadRequest("Invalid API Key");

        try
        {
            // Use SqlConnection to connect to the database
            await using var connection = CreateDbConnection(queryRequest);
            await connection.OpenAsync();
            logger.LogInformation("[/api/test] Ok");
            return Results.Ok();

            DbConnection CreateDbConnection(TestConnectionRequest testRequest)
            {
                if (testRequest.ConnectionType == "postgres")
                    return new NpgsqlConnection(testRequest.ConnectionString);
                // Assuming SQL Server for other types
                return new OdbcConnection(testRequest.ConnectionString);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[/api/test] Error connecting: " + ex.Message);
            return Results.Problem("Error: " + ex.Message);
        }
    }).WithName("Test")
    .WithOpenApi();

// Define the API endpoint
app.MapPost("/api/query",
        async (HttpContext httpContext, QueryRequest queryRequest,
            ILogger<Program> logger) =>
        {
            logger.LogInformation($"[/api/query] {queryRequest}");
            var apiKeyHeader = httpContext.Request.Headers["X-Api-Key"].FirstOrDefault();
            // Validate the API key
            var error = "";
            if (string.IsNullOrEmpty(apiKey))
                error = "Missing X-Api-Key header";
            else if (string.IsNullOrEmpty(apiKey) || apiKey == "{CUSTOM_API_KEY}")
                error = "Update your DataLangServer ApiKey";
            else if (apiKey != apiKeyHeader)
                error = "Invalid API Key";
            else if (forbiddenCommands.Any(command => queryRequest.Sql.ToLower().Contains(command)))
                error = "Forbidden SQL command used.";

            if (!string.IsNullOrEmpty(error))
            {
                logger.LogInformation($"[/api/query] Error: {error}");
                return Results.BadRequest(error);
            }
            try
            {
                DbConnection CreateDbConnection(QueryRequest bodyRequest)
                {
                    if (bodyRequest.ConnectionType == "postgres")
                        return new NpgsqlConnection(bodyRequest.ConnectionString);
                    // Assuming SQL Server for other types
                    return new OdbcConnection(bodyRequest.ConnectionString);
                }

                // Use SqlConnection to connect to the database
                await using var connection = CreateDbConnection(queryRequest);
                await connection.OpenAsync();
                logger.LogInformation("[/api/query] Connected");

                // Execute the query
                await using var command = connection.CreateCommand();
                command.CommandText = queryRequest.Sql;
                await using var reader = await command.ExecuteReaderAsync();

                var results = new List<Dictionary<string, object>>();
                while (await reader.ReadAsync())
                {
                    var data = new Dictionary<string, object>();
                    for (var i = 0; i < reader.FieldCount; i++)
                        // Check if the column value is DBNull.
                        if (reader.IsDBNull(i))
                        {
                            data[reader.GetName(i)] = null!;
                        }
                        else
                        {
                            var value = reader.GetValue(i);
                            // If it's a string, trim it.
                            if (value is string str)
                                data[reader.GetName(i)] = str.Trim();
                            else
                                data[reader.GetName(i)] = value;
                        }

                    results.Add(data);
                }

                logger.LogInformation($"[/api/query] {results.Count} results");

                // Process the results here (e.g., convert them to a suitable format)

                return Results.Ok(new
                {
                    success = true,
                    total_items = results.Count,
                    data = results
                });
            }
            catch (Exception ex)
            {
                logger.LogError("[/api/query] Error in SQL: " + ex.Message);
                return Results.Problem("Error: " + ex.Message);
            }
        }).WithName("ExecuteQuery")
    .WithOpenApi();

app.Run();

internal record TestConnectionRequest(string ConnectionType, string ConnectionString);

internal record QueryRequest(string Sql, string ConnectionType, string ConnectionString);