using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Tenis3t.Data;

// Configuración regional para Colombia
var cultureInfo = new CultureInfo("es-CO");
cultureInfo.NumberFormat.NumberDecimalSeparator = ",";
cultureInfo.NumberFormat.NumberGroupSeparator = ".";
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

var builder = WebApplication.CreateBuilder(args);

// Servicios para controladores y vistas con RuntimeCompilation
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();

// Configuración de Identity
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    // Configuración de opciones de contraseña
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configuración de cookies de autenticación
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Home/AccessDenied";
    options.ReturnUrlParameter = "returnUrl";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);

    // Agregar esta configuración:
    options.Events = new CookieAuthenticationEvents
    {
        OnSignedIn = context =>
        {
            // Redirigir al inventario después de iniciar sesión
            context.HttpContext.Response.Redirect("/Inventario");
            return Task.CompletedTask;
        }
    };
});
// Configuración de DbContext para MySQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString), // Usa AutoDetect para mayor compatibilidad
        mySqlOptions => 
        {
            mySqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
            mySqlOptions.CommandTimeout(180); // Aumenta el timeout si es necesario
        }
    ));
// Registro de servicios personalizados
// builder.Services.AddScoped<IVentaService, VentaService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20); // Tiempo de vida de la sesión
    options.Cookie.HttpOnly = true; // La cookie solo es accesible por el servidor
    options.Cookie.IsEssential = true; // Marca la cookie como esencial
    options.Cookie.Name = "Tenis3t.Session"; // Nombre personalizado para la cookie
});
var app = builder.Build();
// Crear roles al iniciar la aplicación
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        // Crear roles si no existen
        if (!await roleManager.RoleExistsAsync("Admin"))
            await roleManager.CreateAsync(new IdentityRole("Admin"));

        if (!await roleManager.RoleExistsAsync("User"))
            await roleManager.CreateAsync(new IdentityRole("User"));
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error creando roles");
    }
}
// Configuración del pipeline HTTP
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Configuración de endpoints
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();