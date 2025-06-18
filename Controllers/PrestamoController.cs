using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tenis3t.Data;
using Tenis3t.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;

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
        public async Task<IActionResult> Index(string filtro = "todos")
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var query = _context.Prestamos
                .Include(p => p.TallaInventario)
                    .ThenInclude(ti => ti.Inventario)
                .Include(p => p.UsuarioPrestamista)
                .Include(p => p.UsuarioReceptor)
                .AsQueryable();

            // Aplicar filtros
            switch (filtro.ToLower())
            {
                case "realizados":
                    query = query.Where(p => p.UsuarioPrestamistaId == currentUser.Id);
                    break;
                case "recibidos":
                    query = query.Where(p => p.UsuarioReceptorId == currentUser.Id);
                    break;
                case "local":
                    query = query.Where(p => p.TipoPrestamo == "Local");
                    break;
                case "todos":
                default:
                    // Mostrar tanto los realizados como los recibidos
                    query = query.Where(p => p.UsuarioPrestamistaId == currentUser.Id ||
                                           p.UsuarioReceptorId == currentUser.Id);
                    break;
            }

            var prestamos = await query.ToListAsync();
            ViewBag.FiltroSeleccionado = filtro;
            return View(prestamos);
        }

        // GET: Prestamo/Create
        public async Task<IActionResult> Create()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            // Obtener productos disponibles del inventario del usuario actual
            var productos = await _context.Inventarios
                .Where(i => i.UsuarioId == currentUser.Id)
                .Select(i => new ProductoSelectViewModel { Id = i.Id, Nombre = i.Nombre })
                .ToListAsync();

            if (productos == null || !productos.Any())
            {
                TempData["ErrorMessage"] = "No tienes productos en tu inventario. Debes agregar productos primero.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Productos = productos;
            ViewBag.Usuarios = await _userManager.Users
                .Where(u => u.Id != currentUser.Id)
                .Select(u => new UsuarioSelectViewModel { Id = u.Id, UserName = u.UserName })
                .ToListAsync();

            return View(new Prestamo());
        }

        // POST: Prestamo/Create
       [HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Create([Bind("Id,FechaPrestamo,FechaDevolucionEstimada,FechaDevolucionReal,Estado,Cantidad,TallaInventarioId,LocalPersona,TipoPrestamo,UsuarioReceptorId")] Prestamo prestamo)
{
    var currentUser = await _userManager.GetUserAsync(User);

    // Asignar valores que no vienen del formulario
    prestamo.UsuarioPrestamistaId = currentUser.Id;
    prestamo.Estado = "Prestado";
    prestamo.FechaPrestamo = DateTime.Now;

    if (ModelState.IsValid)
    {
        try
        {
            // Validar fecha de devolución
            if (prestamo.FechaDevolucionEstimada <= DateTime.Now)
            {
                ModelState.AddModelError("FechaDevolucionEstimada", "La fecha de devolución debe ser mayor a la fecha actual");
                await LoadCreateViewData(currentUser);
                return View(prestamo);
            }

            // Validar cantidad disponible
            var tallaInventario = await _context.TallasInventario
                .Include(t => t.Inventario)
                .FirstOrDefaultAsync(ti => ti.Id == prestamo.TallaInventarioId);

            if (tallaInventario == null)
            {
                ModelState.AddModelError("TallaInventarioId", "La talla seleccionada no existe");
                await LoadCreateViewData(currentUser);
                return View(prestamo);
            }

            if (tallaInventario.Cantidad < prestamo.Cantidad)
            {
                ModelState.AddModelError("Cantidad", $"No hay suficiente cantidad disponible. Solo quedan {tallaInventario.Cantidad} unidades");
                await LoadCreateViewData(currentUser);
                return View(prestamo);
            }

            // Actualizar inventario
            tallaInventario.Cantidad -= prestamo.Cantidad;
            _context.Update(tallaInventario);

            _context.Add(prestamo);
            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = "Préstamo registrado exitosamente";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear préstamo");
            ModelState.AddModelError("", "Ocurrió un error al registrar el préstamo: " + ex.Message);
            await LoadCreateViewData(currentUser);
            return View(prestamo);
        }
    }

    // Si llegamos aquí, algo falló
    await LoadCreateViewData(currentUser);
    return View(prestamo);
}

        // Actualizar también el método LoadCreateViewData
        private async Task LoadCreateViewData(IdentityUser currentUser)
        {
            ViewBag.Productos = await _context.Inventarios
                .Where(i => i.UsuarioId == currentUser.Id)
                .Select(i => new ProductoSelectViewModel { Id = i.Id, Nombre = i.Nombre })
                .ToListAsync();

            ViewBag.Usuarios = await _userManager.Users
                .Where(u => u.Id != currentUser.Id)
                .Select(u => new UsuarioSelectViewModel { Id = u.Id, UserName = u.UserName })
                .ToListAsync();
        }

        // Método para obtener tallas de un producto (AJAX)
        [HttpGet]
        public async Task<IActionResult> GetTallasByProducto(int productoId)
        {
            var tallas = await _context.TallasInventario
                .Where(ti => ti.InventarioId == productoId && ti.Cantidad > 0)
                .Select(ti => new { ti.Id, ti.Talla, ti.Cantidad })
                .ToListAsync();

            return Json(tallas);
        }

        // Método para obtener cantidad disponible (AJAX)
        [HttpGet]
        public async Task<IActionResult> GetCantidadDisponible(int tallaInventarioId)
        {
            var talla = await _context.TallasInventario
                .FirstOrDefaultAsync(ti => ti.Id == tallaInventarioId);

            if (talla == null)
            {
                return NotFound();
            }

            return Json(new { cantidad = talla.Cantidad });
        }
    }
}