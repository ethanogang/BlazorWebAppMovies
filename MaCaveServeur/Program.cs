using MaCaveServeur.Data;
using MaCaveServeur.Services;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------------------
// EPPlus (Excel) : licence non commerciale (évite l'exception à l'import/export)
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

// -----------------------------------------------------------------------------
// EF Core + SQLite
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// -----------------------------------------------------------------------------
// Services applicatifs
builder.Services.AddScoped<CellarService>();
builder.Services.AddScoped<SupplierService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<AppState>();

// (optionnel) Accès HTTP & Session si tu en as besoin plus tard
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession();

// Blazor
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

var app = builder.Build();

// -----------------------------------------------------------------------------
// Création / migration de la base au démarrage (recommandé en dev)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // ► Choisis UNE des deux lignes ci-dessous :

    // 1) Méthode PROPRE (migrations). Assure-toi d'avoir lancé :
    //    dotnet ef migrations add Init
    //    dotnet ef database update
    db.Database.Migrate();

    // 2) Méthode EXPRESS (sans migrations, utile en dev uniquement) :
    // db.Database.EnsureCreated();
}

// -----------------------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();

// Hub Blazor + fallback
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
