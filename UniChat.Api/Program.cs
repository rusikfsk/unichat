using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using UniChat.Api.Hubs;
using UniChat.Api.Services;
using UniChat.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DB
builder.Services.AddDbContext<UniChatDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// SignalR
builder.Services.AddSignalR();

// CORS (Vite dev)
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("dev", p =>
        p.WithOrigins("http://localhost:5173")
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials());
});

// JWT services
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            NameClaimType = ClaimTypes.NameIdentifier
        };

        // SignalR token via query-string
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// cleanup service 
builder.Services.AddHostedService<UnboundAttachmentsCleanupService>();

var app = builder.Build();

// Единый обработчик ошибок (кроме Development)
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var feature = context.Features.Get<IExceptionHandlerFeature>();
            var ex = feature?.Error;

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://httpstatuses.com/500",
                title = "Server error",
                status = 500,
                detail = "Unexpected server error."
            });
        });
    });

    app.UseHsts();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("dev");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

app.Run();
