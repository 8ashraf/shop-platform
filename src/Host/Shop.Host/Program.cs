using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Shop.Host;
using Shop.Shared.Infrastructure;
using Shop.Shared.Kernel;

var builder = WebApplication.CreateBuilder(args);

var modules = ModuleCatalog.All;

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();

// ---------- Swagger مع زر Authorize ----------
builder.Services.AddSwaggerGen(options =>
{
    var bearerScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "الصق الـ accessToken هنا (من غير كلمة Bearer ومن غير تنصيص)",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };

    options.AddSecurityDefinition("Bearer", bearerScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { bearerScheme, Array.Empty<string>() }
    });
});

// ---------- فك الـ JWT في كل طلب ----------
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Configuration 'Jwt:Key' is missing.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)//شرط: كل طلب لازم يكون فيه Authorization header فيه Bearer token
    .AddJwtBearer(options =>
    {
        // نخلّي أسماء الـ claims زي ما هي (sub, account_id...)
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,// لازم الـ token يكون جاي من نفس الـ issuer اللي احنا محددينه
            ValidIssuer = builder.Configuration["Jwt:Issuer"],// لازم الـ token يكون موجه للـ audience اللي احنا محددينه
            ValidateAudience = true,// لازم الـ token يكون موجه للـ audience اللي احنا محددينه
            ValidAudience = builder.Configuration["Jwt:Audience"],// لازم الـ token يكون موقّع بالمفتاح السري اللي احنا محددينه
            ValidateLifetime = true,// لازم الـ token يكون مش منتهي الصلاحية
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30)// السماح بفارق 30 ثانية بين ساعة السيرفر وساعة الـ token (عشان لو السيرفر شوية متأخر أو متقدم عن ساعة الـ token)
        };
    });

builder.Services.AddAuthorization(options =>
{
    // موظفي المنصة بس
    options.AddPolicy("PlatformStaffOnly", policy =>
        policy.RequireClaim("account_type", "PlatformStaff"));

    // سياسة لكل صلاحية، واسمها هو اسم الصلاحية نفسها
    foreach (var permission in Permissions.All)
        options.AddPolicy(permission, policy => policy.RequireClaim("perms", permission));
});

builder.Services.AddSharedInfrastructure();
builder.Services.AddModules(builder.Configuration, modules);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();

// الترتيب مهم: الأول نعرف إنت مين، وبعدين نقرر مسموح لك ولا لأ
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapModules(modules);

app.Run();

/// <summary>Exposed for WebApplicationFactory in integration tests.</summary>
public partial class Program
{
}