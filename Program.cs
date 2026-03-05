using PropertySearchAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<SearchService>();
builder.Services.AddSingleton<CsvAreaService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<OpenAIIntentService>();
builder.Services.AddSingleton<Property24ScraperService>();
builder.Services.AddHttpClient<OxylabsFetchService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.MapControllers();

app.Run();