using Amazon.Lambda.AspNetCoreServer.Hosting;
using NotificationPlatform.Api.Middleware;
using NotificationPlatform.Api.Validation;
using NotificationPlatform.DataAccess;
using NotificationPlatform.Integrations;
using NotificationPlatform.Services;

var builder = WebApplication.CreateBuilder(args);

// Layers. Each project registers its own classes; options are validated on startup.
builder.Services
    .AddServices()
    .AddDataAccess(builder.Configuration)
    .AddIntegrations(builder.Configuration);

// B2: authentication schemes (Bearer default + ApiKey).
// B3.2 / B5.1: queue selection (SQS when Queue:Url is set, otherwise the local queue and worker).

// Controllers. System.Text.Json uses camelCase by default.
builder.Services.AddControllers()
    // Don't echo raw JSON parser messages (they contain internal type names); use a generic message instead.
    .AddJsonOptions(options => options.AllowInputFormatterExceptionMessages = false)
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = ValidationErrorResponse.Create; // 400 -> Error shape
        options.SuppressMapClientErrors = true; // keep NotFound()/Unauthorized() bodiless, see UseStatusCodePages
    });

// Runs the same app inside Lambda behind API Gateway (HTTP API). No effect under Kestrel.
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

var app = builder.Build();

// Create MongoDB indexes once at startup (idempotent; failures are logged, not fatal).
await app.Services.GetRequiredService<MongoIndexInitializer>().EnsureIndexesAsync();

app.UseMiddleware<ExceptionHandlingMiddleware>();        // unhandled exception -> 500 Error shape
app.UseStatusCodePages(ErrorStatusCodePages.WriteAsync); // bodiless 4xx -> Error shape
// B2: app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
