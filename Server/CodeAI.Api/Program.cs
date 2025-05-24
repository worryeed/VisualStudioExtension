using CodeAI.Api.Data;
using CodeAI.Api.Services;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

namespace CodeAI.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var cfg = builder.Configuration;

        builder.Services.AddAuthentication(opt =>
        {
            opt.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
            .AddCookie("External", opt =>
            {
                opt.Cookie.Name = "CodeAI.External";
                opt.Cookie.SameSite = SameSiteMode.Lax;
                opt.ExpireTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddJwtBearer(opt =>
            {
                var jwt = cfg.GetSection("Jwt");
                var key = jwt["Key"]!;
                opt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt["Issuer"],
                    ValidAudience = jwt["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
                };
            })
            .AddGitHub("github", opt =>
            {
                var gh = cfg.GetSection("GitHub");
                opt.ClientId = gh["ClientId"]!;
                opt.ClientSecret = gh["ClientSecret"]!;
                opt.CallbackPath = "/signin-github";
                opt.SignInScheme = "External";
                opt.SaveTokens = true;

                opt.Events = new OAuthEvents
                {
                    OnRemoteFailure = ctx =>
                    {
                        var logger = ctx.HttpContext.RequestServices
                                          .GetRequiredService<ILoggerFactory>()
                                          .CreateLogger("OAuthRemoteFailure");
                        logger.LogError(ctx.Failure, "GitHub OAuth failed");

                        ctx.Response.StatusCode = 500;
                        return ctx.Response.WriteAsync(
                            $"OAuth error: {ctx.Failure?.Message}");
                    }
                };
            });

        builder.Services.AddRateLimiter(options =>
        {
            options.AddPolicy("ai-requests", httpContext =>
            {
                var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? httpContext.Connection.RemoteIpAddress?.ToString()
                             ?? "anonymous";

                return RateLimitPartition.GetTokenBucketLimiter(partitionKey: userId, factory: _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 15,
                    TokensPerPeriod = 5,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
            });

            options.OnRejected = (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                return new ValueTask();
            };
        });

        builder.Services.AddMassTransit(cfg =>
        {
            cfg.AddConsumer<CodeGenConsumer>();

            cfg.UsingRabbitMq((ctx, bus) =>
            {
                bus.Host("rabbitmq", "/", h =>
                {
                    h.Username("guest");
                    h.Password("guest");
                });

                bus.ConfigureEndpoints(ctx);
            });

            cfg.SetDefaultRequestTimeout(s: 100);
        });

        builder.Services.AddCors(o =>
        {
            o.AddPolicy("allow-all", p => p
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod());
        });

        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
            .Enrich.FromLogContext()
            .CreateLogger();

        builder.Host.UseSerilog();

        // PostgreSQL
        builder.Services.AddDbContext<AppDbContext>(opt =>
            opt.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

        // Redis
        builder.Services.AddStackExchangeRedisCache(opt =>
            opt.Configuration = builder.Configuration.GetSection("Redis")["Configuration"]);

        builder.Services.AddHttpClient<IAIService, OllamaService>();
        builder.Services.AddSingleton<IRedisCacheService, RedisCacheService>();
        builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
        builder.Services.AddScoped<ITokenService, TokenService>();
        builder.Services.AddControllers();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        }

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors("allow-all");
        app.UseRateLimiter();
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        Log.Information("Starting up");
        try { app.Run(); }
        finally { Log.Information("Shutting down"); Log.CloseAndFlush(); }
    }
}
