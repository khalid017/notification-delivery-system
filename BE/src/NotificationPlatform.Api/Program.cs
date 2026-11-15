using Amazon.Lambda.AspNetCoreServer.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using NotificationPlatform.Api.Middleware;
using NotificationPlatform.Api.Validation;
using NotificationPlatform.DataAccess;
using NotificationPlatform.Integrations;
using NotificationPlatform.Services;
using NotificationPlatform.Services.Auth;

var builder = WebApplication.CreateBuilder(args);

// Layers. Each project registers its own classes; options are validated on startup.
builder.Services
    .AddServices()
    .AddDataAccess(builder.Configuration)
    .AddIntegrations(builder.Configuration);

// Dashboard JWT settings. Bound here, not in AddServices(), because the Worker has no JWT secret.
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Authentication. "Bearer" (dashboard JWT) is the default scheme. B2.3 adds the "ApiKey" scheme.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<TokenService>((options, tokenService) =>
    {
        options.MapInboundClaims = false; // keep "sub" and "tenant_id" as written, not mapped to long URIs
        options.TokenValidationParameters = tokenService.CreateValidationParameters(); // one definition (B2.1)
    });

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
app.UseAuthentication(); // who is calling (builds HttpContext.User)
app.UseAuthorization();  // may they call this endpoint ([Authorize])

app.MapControllers();

app.Run();
