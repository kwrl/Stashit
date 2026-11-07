using Stashit.Configuration;
using Stashit.Infrastructure;
using Stashit.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.Configure<LimitsOptions>(builder.Configuration.GetSection(LimitsOptions.SectionName));

builder.Services.AddStashing();
builder.Services.AddControllers();

var app = builder.Build();

app.UseRouting();
app.MapControllers();

app.Run();
