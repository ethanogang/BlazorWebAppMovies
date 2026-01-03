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
builder.Services.AddSingleton<MovementLogService>();

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
// Import CLI
if (args.Length >= 2 && args[0].Equals("--import-suppliers", StringComparison.OrdinalIgnoreCase))
{
    var path = args[1];
    if (!File.Exists(path))
    {
        Console.WriteLine($"Fichier introuvable: {path}");
        return;
    }

    using var scope = app.Services.CreateScope();
    var svc = scope.ServiceProvider.GetRequiredService<SupplierService>();
    using var fs = File.OpenRead(path);
    var result = svc.ImportExcelAsync(fs).GetAwaiter().GetResult();
    Console.WriteLine($"Import fournisseurs termine: {result.inserted} ajout(s), {result.updated} mise(s) a jour.");
    return;
}

if (args.Length >= 1 && args[0].Equals("--dedupe-suppliers", StringComparison.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var svc = scope.ServiceProvider.GetRequiredService<SupplierService>();
    var result = svc.DedupeAsync().GetAwaiter().GetResult();
    Console.WriteLine($"Dedoublonnage fournisseurs: {result.merged} fusion(s), {result.removed} suppression(s).");
    return;
}

if (args.Length >= 3 && args[0].Equals("--sync-suppliers", StringComparison.OrdinalIgnoreCase))
{
    var suppliersPath = args[1];
    var masterPath = args[2];
    if (!File.Exists(suppliersPath))
    {
        Console.WriteLine($"Fichier introuvable: {suppliersPath}");
        return;
    }
    if (!File.Exists(masterPath))
    {
        Console.WriteLine($"Fichier introuvable: {masterPath}");
        return;
    }

    using var scope = app.Services.CreateScope();
    var svc = scope.ServiceProvider.GetRequiredService<SupplierService>();
    using var supFs = File.OpenRead(suppliersPath);
    using var masterFs = File.OpenRead(masterPath);
    var result = svc.SyncFromMasterAsync(supFs, masterFs).GetAwaiter().GetResult();
    Console.WriteLine($"Sync fournisseurs: {result.removedSuppliers} supprime(s), {result.addedSuppliers} ajoute(s), {result.updatedBottles} bouteille(s) maj.");
    return;
}

if (args.Length >= 1 && args[0].Equals("--delete-archiver-name", StringComparison.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var toDelete = db.Bottles
        .Where(b => b.Name != null && b.Name.ToLower().Contains("archiver"))
        .ToList();
    db.Bottles.RemoveRange(toDelete);
    db.SaveChanges();
    Console.WriteLine($"Suppression bouteilles (Name contient 'archiver'): {toDelete.Count}");
    return;
}

if (args.Length >= 2 && args[0].Equals("--delete-invalid-suppliers", StringComparison.OrdinalIgnoreCase))
{
    var suppliersPath = args[1];
    if (!File.Exists(suppliersPath))
    {
        Console.WriteLine($"Fichier introuvable: {suppliersPath}");
        return;
    }

    using var scope = app.Services.CreateScope();
    var svc = scope.ServiceProvider.GetRequiredService<SupplierService>();
    using var supFs = File.OpenRead(suppliersPath);
    var deleted = svc.DeleteBottlesWithInvalidSupplierAsync(supFs).GetAwaiter().GetResult();
    Console.WriteLine($"Suppression bouteilles fournisseur invalide: {deleted}");
    return;
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
