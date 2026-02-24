using JobTracker.Core.Interfaces.Repositories;
using JobTracker.Core.Interfaces.Services;
using JobTracker.Infrastructure.Background;
using JobTracker.Infrastructure.Data;
using JobTracker.Infrastructure.Repositories;
using JobTracker.Infrastructure.Services;
using JobTracker.Infrastructure.Services.EmailClient;
using JobTracker.Infrastructure.Services.LLM;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<JobTrackerDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=jobtracker.db"));

// Repositories
builder.Services.AddScoped<IApplicationRepository, ApplicationRepository>();
builder.Services.AddScoped<IEmailRepository, EmailRepository>();
builder.Services.AddScoped<IEmailAccountRepository, EmailAccountRepository>();

// Services
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<IEmailProcessingService, EmailProcessingService>();
builder.Services.AddScoped<IEmailAccountService, EmailAccountService>();
builder.Services.AddScoped<IEmailService, MailKitEmailService>();

// Google OAuth Service
builder.Services.AddHttpClient<IGoogleOAuthService, GoogleOAuthService>();

// Ollama LLM Service
builder.Services.AddHttpClient<ILLMService, OllamaLLMService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434");
    client.Timeout = TimeSpan.FromMinutes(2);
});

// Sync orchestrator — singleton so controller + polling service share the same lock/status
builder.Services.AddSingleton<EmailSyncOrchestrator>();

// Background Services
builder.Services.AddHostedService<EmailPollingService>();

// Data Protection (for password encryption)
builder.Services.AddDataProtection();

// Controllers + Swagger
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:4200" };
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Ensure database is created and schema is up to date
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<JobTrackerDbContext>();
    db.Database.EnsureCreated();

    // Use DELETE journal mode so external SQLite GUI tools can access the DB
    // while the app is running (WAL mode locks the file for other processes)
    var conn = db.Database.GetDbConnection();
    await conn.OpenAsync();
    using (var pragmaCmd = conn.CreateCommand())
    {
        pragmaCmd.CommandText = "PRAGMA journal_mode=DELETE";
        await pragmaCmd.ExecuteNonQueryAsync();
    }

    // Add OAuth columns to EmailAccounts if they don't exist (SQLite doesn't support EnsureCreated for schema updates)
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "PRAGMA table_info(EmailAccounts)";
    var columns = new HashSet<string>();
    using (var reader = await cmd.ExecuteReaderAsync())
    {
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(1));
    }

    async Task AddColumnIfMissing(string column, string type, string defaultValue = "")
    {
        if (!columns.Contains(column))
        {
            using var alter = conn.CreateCommand();
            alter.CommandText = $"ALTER TABLE EmailAccounts ADD COLUMN {column} {type}{defaultValue}";
            await alter.ExecuteNonQueryAsync();
        }
    }

    await AddColumnIfMissing("AuthType", "TEXT", " DEFAULT 'Password'");
    await AddColumnIfMissing("EncryptedRefreshToken", "TEXT", "");
    await AddColumnIfMissing("AccessToken", "TEXT", "");
    await AddColumnIfMissing("TokenExpiresAt", "TEXT", "");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();

app.Run();
