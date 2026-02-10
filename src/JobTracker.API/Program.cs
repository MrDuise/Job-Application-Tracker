using JobTracker.Core.Interfaces.Repositories;
using JobTracker.Core.Interfaces.Services;
using JobTracker.Infrastructure.Background;
using JobTracker.Infrastructure.Data;
using JobTracker.Infrastructure.Repositories;
using JobTracker.Infrastructure.Services;
using JobTracker.Infrastructure.Services.Email;
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

// Ollama LLM Service
builder.Services.AddHttpClient<ILLMService, OllamaLLMService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434");
    client.Timeout = TimeSpan.FromMinutes(2);
});

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

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<JobTrackerDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();

app.Run();
