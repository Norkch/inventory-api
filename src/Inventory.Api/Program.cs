using Inventory.Api.Authorization;
using Inventory.Api.Configuration;
using Inventory.Api.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddValidatedAuthOptions(builder.Configuration);
builder.Services.AddValidatedDatabaseOptions(builder.Configuration);
builder.Services.AddPersistence();
builder.Services.AddCategories();
builder.Services.AddProducts();
builder.Services.AddMovements();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.ConfigureOptions<ConfigureJwtBearerOptions>();
builder.Services.AddAuthorization(options => options.AddScopePolicies());
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = InvalidModelStateResponseFactory.Create);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureOptions<ConfigureSwaggerGenOptions>();
builder.Services.ConfigureOptions<ConfigureSwaggerUIOptions>();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
