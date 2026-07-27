using CandidateService.API.Middlewares;
using CandidateService.Application.Commands;
using CandidateService.Application.Interfaces;
using CandidateService.Domain.Interfaces;
using CandidateService.Infrastructure.Data;
using CandidateService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {OperationId} {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=/data/candidate.db"));

builder.Services.AddScoped<IOperationRepository, OperationRepository>();

builder.Services.AddHttpClient<IProviderService, ProviderService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["PROVIDER_URL"] ?? "http://provider-simulator:8081");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
builder.Services.AddHostedService<BackgroundTaskService>();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateOperationCommand).Assembly));

builder.Services.AddScoped<SubmitOperationCommandHandler>();


builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.EnsureCreated();
}

await RecoverPendingOperations(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseRouting();
app.MapControllers();

app.Lifetime.ApplicationStopping.Register(() =>
{
    Log.Information("Application is stopping. Waiting for background tasks to complete...");
    Thread.Sleep(5000);
    Log.Information("Application stopped");
});

await app.RunAsync();

async Task RecoverPendingOperations(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var repository = scope.ServiceProvider.GetRequiredService<IOperationRepository>();
    var backgroundQueue = scope.ServiceProvider.GetRequiredService<IBackgroundTaskQueue>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var pendingOperations = await repository.GetProcessingOperationsAsync();

        if (pendingOperations.Any())
        {
            logger.LogInformation("Found {Count} pending operations to recover", pendingOperations.Count());

            foreach (var operation in pendingOperations)
            {
                logger.LogInformation("Recovering operation {OperationId} on startup", operation.Id);
                backgroundQueue.QueueBackgroundWorkItem(async (sp, ct) =>
                {
                    using var taskScope = sp.CreateScope();

                    var handler = taskScope.ServiceProvider.GetRequiredService<SubmitOperationCommandHandler>();
                    await handler.ProcessOperationAsync(sp, operation.Id, ct);
                });
            }
        }
        else
        {
            logger.LogInformation("No pending operations to recover");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error recovering pending operations");
    }
}