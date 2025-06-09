using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tenis3t.Data;
using Tenis3t.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Tenis3t.Controllers
{
    [Authorize]
    public class PrestamoController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<PrestamoController> _logger;

        public PrestamoController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            ILogger<PrestamoController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: Prestamo
        public async Task<IActionResult> Index(
            string tipo = "todos",
            string filtroNombre = "",
            DateTime? filtroFecha = null)
        {
            try
            {
                var usuarioActual = await _userManager.GetUserAsync(User);
                if (usuarioActual == null)
                {
                    return Challenge(); // Redirige al login si no hay usuario autenticado
                }

                // Construir consulta base con proyección explícita
                var query = _context.Prestamos
                    .Include(p => p.TallaInventario)
                        .ThenInclude(t => t.Inventario)
                    .Include(p => p.UsuarioPrestamista)
                    .Include(p => p.UsuarioReceptor)
                    .Where(p => p.UsuarioPrestamistaId == usuarioActual.Id ||
                               p.UsuarioReceptorId == usuarioActual.Id)
                    .AsQueryable();

                // Aplicar filtros
                query = AplicarFiltros(query, tipo, filtroNombre, filtroFecha, usuarioActual.Id);

                // Configurar ViewBag
                ConfigurarViewBag(tipo, filtroNombre, filtroFecha);

                var prestamos = await query
                    .OrderByDescending(p => p.FechaPrestamo)
                    .ToListAsync();

                return View(prestamos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar los préstamos");
                TempData["ErrorMessage"] = "Ocurrió un error al cargar los préstamos";
                return RedirectToAction("Index", "Home");
            }
        }

        private IQueryable<Prestamo> AplicarFiltros(
            IQueryable<Prestamo> query,
            string tipo,
            string filtroNombre,
            DateTime? filtroFecha,
            string usuarioId)
        {
            if (!string.IsNullOrEmpty(filtroNombre))
            {
                query = query.Where(p => p.LocalPersona.Contains(filtroNombre));
            }

            if (filtroFecha.HasValue)
            {
                query = query.Where(p => p.FechaPrestamo.Date == filtroFecha.Value.Date);
            }

            switch (tipo)
            {
                case "realizados":
                    query = query.Where(p => p.UsuarioPrestamistaId == usuarioId);
                    break;
                case "recibidos":
                    query = query.Where(p => p.UsuarioReceptorId == usuarioId);
                    break;
                case "todos":
                default:
                    // No se aplica filtro adicional
                    break;
            }

            return query;
        }

        private void ConfigurarViewBag(string tipo, string filtroNombre, DateTime? filtroFecha)
        {
            ViewBag.TipoSeleccionado = tipo;
            ViewBag.FiltroNombre = filtroNombre;
            ViewBag.FiltroFecha = filtroFecha?.ToString("yyyy-MM-dd");
        }
        // GET: Prestamo/Create
        public async Task<IActionResult> Create(string tipo)
        {
            if (tipo != "realizados" && tipo != "recibidos")
            {
                return RedirectToAction(nameof(Index));
            }

            var usuarioActual = await _userManager.GetUserAsync(User);
            var model = new Prestamo
            {
                TipoPrestamo = tipo == "realizados" ? "Realizado" : "Recibido",
                FechaPrestamo = DateTime.Now
            };

            await LoadViewBagData(usuarioActual.Id, model.TipoPrestamo);

            // Cargar lista de otros usuarios (locales)
            ViewBag.Usuarios = await _userManager.Users
                .Where(u => u.Id != usuarioActual.Id)
                .Select(u => u.UserName)
                .ToListAsync();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetTallasByProducto(int productoId)
        {
            try
            {
                var usuarioActual = await _userManager.GetUserAsync(User);

                var productoExiste = await _context.Inventarios
                    .AnyAsync(i => i.Id == productoId && i.UsuarioId == usuarioActual.Id);

                if (!productoExiste)
                {
                    return NotFound(new { error = "Producto no encontrado o no tienes permisos" });
                }

                var tallas = await _context.TallasInventario
                    .Where(t => t.InventarioId == productoId && t.Cantidad > 0)
                    .Select(t => new { t.Id, t.Talla, t.Cantidad })
                    .ToListAsync();

                return Json(tallas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener tallas por producto");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Create(Prestamo prestamo)
{
    _logger.LogInformation("Iniciando creación de préstamo...");
    
    try
    {
        var usuarioActual = await _userManager.GetUserAsync(User);
        if (usuarioActual == null) return Challenge();

        // Validación básica
        if (prestamo.TipoPrestamo != "Realizado" && prestamo.TipoPrestamo != "Recibido")
        {
            TempData["ErrorMessage"] = "Tipo de préstamo no válido";
            return RedirectToAction(nameof(Index));
        }

        _logger.LogInformation($"Validando receptor: {prestamo.LocalPersona}");
        var usuarioReceptor = await _userManager.FindByNameAsync(prestamo.LocalPersona);

        if (usuarioReceptor == null)
        {
            ModelState.AddModelError("LocalPersona", "El local/persona no existe");
            await CargarDatosVista(usuarioActual.Id, prestamo.TipoPrestamo);
            return View(prestamo);
        }

        // Asignar valores automáticos ANTES de la validación
        prestamo.UsuarioPrestamistaId = usuarioActual.Id;
        prestamo.UsuarioReceptorId = usuarioReceptor.Id;
        prestamo.FechaPrestamo = DateTime.Now;
        prestamo.Estado = "Prestado";

        // Limpiar validación de propiedades de navegación
        ModelState.Remove("UsuarioPrestamista");
        ModelState.Remove("UsuarioReceptor");
        ModelState.Remove("TallaInventario");
        ModelState.Remove("Inventario");

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Modelo no válido. Errores: " + 
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            await CargarDatosVista(usuarioActual.Id, prestamo.TipoPrestamo);
            return View(prestamo);
        }

        _logger.LogInformation($"Verificando stock para talla ID: {prestamo.TallaInventarioId}");
        var talla = await _context.TallasInventario
            .Include(t => t.Inventario)
            .FirstOrDefaultAsync(t => t.Id == prestamo.TallaInventarioId);

        if (talla == null || talla.Inventario.UsuarioId != usuarioActual.Id)
        {
            ModelState.AddModelError("", "Talla no válida o sin permisos");
            await CargarDatosVista(usuarioActual.Id, prestamo.TipoPrestamo);
            return View(prestamo);
        }

        if (talla.Cantidad < prestamo.Cantidad)
        {
            ModelState.AddModelError("Cantidad", $"Stock insuficiente. Disponible: {talla.Cantidad}");
            await CargarDatosVista(usuarioActual.Id, prestamo.TipoPrestamo);
            return View(prestamo);
        }

        _logger.LogInformation("Iniciando transacción...");
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Actualizar stock
            talla.Cantidad -= prestamo.Cantidad;
            _context.Update(talla);

            // Crear préstamo
            _context.Add(prestamo);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Préstamo creado exitosamente");
            TempData["SuccessMessage"] = "¡Préstamo registrado exitosamente!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error en transacción de préstamo");
            ModelState.AddModelError("", "Error al guardar el préstamo: " + ex.Message);
            await CargarDatosVista(usuarioActual.Id, prestamo.TipoPrestamo);
            return View(prestamo);
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error general en Create Prestamo");
        TempData["ErrorMessage"] = "Error inesperado. Intente nuevamente.";
        return RedirectToAction(nameof(Create), new { tipo = prestamo?.TipoPrestamo?.ToLower() ?? "realizados" });
    }
}
        private async Task CargarDatosVista(string usuarioId, string tipoPrestamo)
        {
            ViewBag.Productos = await _context.Inventarios
                .Where(i => i.UsuarioId == usuarioId && i.Tallas.Any(t => t.Cantidad > 0))
                .ToListAsync();

            ViewBag.Usuarios = await _userManager.Users
                .Where(u => u.Id != usuarioId)
                .Select(u => u.UserName)
                .ToListAsync();

            ViewBag.TipoPrestamo = tipoPrestamo;
        }

        // GET: Prestamo/Devolver/5
        public async Task<IActionResult> Devolver(int id)
        {
            var usuarioActual = await _userManager.GetUserAsync(User);
            var prestamo = await _context.Prestamos
                .Include(p => p.TallaInventario)
                .FirstOrDefaultAsync(p => p.Id == id &&
                                      (p.UsuarioPrestamistaId == usuarioActual.Id || p.UsuarioReceptorId == usuarioActual.Id) &&
                                      p.Estado == "Prestado");

            if (prestamo == null)
            {
                return NotFound();
            }

            return View(prestamo);
        }

        [HttpPost, ActionName("Devolver")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DevolverConfirmado(int id)
        {
            var usuarioActual = await _userManager.GetUserAsync(User);
            var prestamo = await _context.Prestamos
                .Include(p => p.TallaInventario)
                .FirstOrDefaultAsync(p => p.Id == id &&
                                      (p.UsuarioPrestamistaId == usuarioActual.Id || p.UsuarioReceptorId == usuarioActual.Id) &&
                                      p.Estado == "Prestado");

            if (prestamo == null)
            {
                return NotFound();
            }

            try
            {
                prestamo.Estado = "Devuelto";
                prestamo.FechaDevolucionReal = DateTime.Now;

                // Solo aumentar el stock si el préstamo fue realizado por el usuario actual
                if (prestamo.UsuarioPrestamistaId == usuarioActual.Id)
                {
                    prestamo.TallaInventario.Cantidad += prestamo.Cantidad;
                }

                _context.Update(prestamo);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Préstamo marcado como devuelto";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al devolver préstamo");
                TempData["ErrorMessage"] = "Ocurrió un error al procesar la devolución";
                return RedirectToAction(nameof(Devolver), new { id });
            }
        }

        private async Task LoadViewBagData(string usuarioId, string tipoPrestamo)
        {
            ViewBag.Productos = await _context.Inventarios
                .Where(i => i.UsuarioId == usuarioId)
                .Include(i => i.Tallas)
                .Where(i => i.Tallas.Any(t => t.Cantidad > 0))
                .ToListAsync();
            ViewBag.TipoPrestamo = tipoPrestamo;
        }

        private async Task<List<string>> GetOtherUsers(string currentUserId)
        {
            return await _userManager.Users
                .Where(u => u.Id != currentUserId)
                .Select(u => u.UserName)
                .ToListAsync();
        }
    }
}