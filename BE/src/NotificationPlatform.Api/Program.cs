using NotificationPlatform.DataAccess;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDataAccess(builder.Configuration);

var app = builder.Build();

// Create MongoDB indexes once at startup (idempotent; failures are logged, not fatal).
await app.Services.GetRequiredService<MongoIndexInitializer>().EnsureIndexesAsync();

app.UseAuthorization();

app.MapControllers();

app.Run();
