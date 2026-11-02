using System.Text.Json.Serialization;
using Dispatch.Api.Data;
using Dispatch.Api.Errors;
using Dispatch.Api.Hubs;
using Dispatch.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services
    .AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks().AddDbContextCheck<DispatchDbContext>();

builder.Services.AddDispatchDatabase(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IDispatchNotifier, SignalRDispatchNotifier>();
builder.Services.AddScoped<IncidentService>();
builder.Services.AddScoped<UnitService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment()))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHub<DispatchHub>("/hubs/dispatch");
app.MapHealthChecks("/health");

await app.InitializeDatabaseAsync();
app.Run();

// Lets the integration tests reference the app via WebApplicationFactory<Program>.
public partial class Program;
