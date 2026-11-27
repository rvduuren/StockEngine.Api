using StockEngine.Api.Endpoints;
using StockEngine.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.AddStockEngineServices();

var app = builder.Build();

await app.PrepareDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapApiEndpoints();
app.MapAdminUserEndpoints();

app.Run();

public partial class Program;