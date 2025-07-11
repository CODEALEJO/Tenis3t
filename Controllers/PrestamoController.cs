using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tenis3t.Data;
using Tenis3t.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using Tenis3t.Models.DTOs;

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

            var prestamos = await query.OrderByDescending(p => p.FechaPrestamo).ToListAsync();
            ViewBag.FiltroSeleccionado = filtro;
            return View(prestamos);
        }

        // GET: Prestamo/Create
        public async Task<IActionResult> Create()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            await CargarDatosParaCrear(currentUser);

            // Verificar si hay productos disponibles
            var productos = ViewBag.Productos as List<ProductoSelectViewModel>;
            if (productos == null || !productos.Any())
            {
                TempData["ErrorMessage"] = "No tienes productos en tu inventario. Debes agregar productos primero.";
                return RedirectToAction(nameof(Index));
            }

            var dto = new CrearPrestamoDto
            {

                Cantidad = 1
            };

            return View(dto);
        }

        // POST: Prestamo/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CrearPrestamoDto dto)
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (!ModelState.IsValid)
            {
                await CargarDatosParaCrear(currentUser);
                return View(dto);
            }

            try
            {
                // Validar disponibilidad
                var talla = await _context.TallasInventario
                    .Include(t => t.Inventario)
                    .FirstOrDefaultAsync(t => t.Id == dto.TallaInventarioId);

                if (talla == null || talla.Inventario.UsuarioId != currentUser.Id)
                {
                    ModelState.AddModelError("", "No tienes permiso para prestar este producto");
                    await CargarDatosParaCrear(currentUser);
                    return View(dto);
                }

                if (talla.Cantidad < dto.Cantidad)
                {
                    ModelState.AddModelError("Cantidad", $"Solo hay {talla.Cantidad} unidades disponibles");
                    await CargarDatosParaCrear(currentUser);
                    return View(dto);
                }

                // Crear préstamo
                var prestamo = new Prestamo
                {
                    UsuarioPrestamistaId = currentUser.Id,
                    UsuarioReceptorId = dto.UsuarioReceptorId,
                    TallaInventarioId = dto.TallaInventarioId,
                    Cantidad = dto.Cantidad,
                    Estado = "Prestado"
                };

                // Actualizar inventario
                talla.Cantidad -= dto.Cantidad;

                _context.Add(prestamo);
                _context.Update(talla);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Préstamo creado exitosamente";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear préstamo");
                ModelState.AddModelError("", "Error al crear el préstamo");
                await CargarDatosParaCrear(currentUser);
                return View(dto);
            }
        }



        // Método privado para crear el préstamo en la base de datos
        private async Task CrearPrestamoEnBaseDeDatos(CrearPrestamoDto dto, IdentityUser currentUser)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Obtener la talla de inventario
                var tallaInventario = await _context.TallasInventario
                    .FirstOrDefaultAsync(ti => ti.Id == dto.TallaInventarioId);

                if (tallaInventario == null)
                    throw new InvalidOperationException("Talla de inventario no encontrada");

                // Crear el préstamo
                var prestamo = new Prestamo
                {
                    UsuarioPrestamistaId = currentUser.Id,
                    UsuarioReceptorId = dto.UsuarioReceptorId,
                    TallaInventarioId = dto.TallaInventarioId,
                    Cantidad = dto.Cantidad,
                    Estado = "Prestado",
                    FechaPrestamo = DateTime.Now,
                    TipoPrestamo = "Persona",
                    LocalPersona = "N/A"
                };

                // Actualizar inventario
                tallaInventario.Cantidad -= dto.Cantidad;

                // Guardar cambios
                _context.Add(prestamo);
                _context.Update(tallaInventario);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                _logger.LogInformation($"Préstamo creado exitosamente. ID: {prestamo.Id}");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // Método privado para cargar datos para la vista
        private async Task CargarDatosParaCrear(IdentityUser currentUser)
        {
            try
            {
                // Productos ordenados alfabéticamente (A-Z) por Nombre
                var productos = await _context.Inventarios
                    .Where(i => i.UsuarioId == currentUser.Id)
                    .OrderBy(i => i.Nombre) // Orden alfabético
                    .Select(i => new ProductoSelectViewModel { Id = i.Id, Nombre = i.Nombre })
                    .ToListAsync();

                // Usuarios ordenados alfabéticamente (A-Z) por UserName
                var usuarios = await _userManager.Users
                    .Where(u => u.Id != currentUser.Id)
                    .OrderBy(u => u.UserName) // Orden alfabético
                    .Select(u => new UsuarioSelectViewModel { Id = u.Id, UserName = u.UserName })
                    .ToListAsync();

                ViewBag.Productos = productos ?? new List<ProductoSelectViewModel>();
                ViewBag.Usuarios = usuarios ?? new List<UsuarioSelectViewModel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar datos para Create");
                ViewBag.Productos = new List<ProductoSelectViewModel>();
                ViewBag.Usuarios = new List<UsuarioSelectViewModel>();
            }
        }

        // Método privado para loggear errores de validación
        private void LogearErroresValidacion()
        {
            foreach (var state in ModelState)
            {
                if (state.Value.Errors.Count > 0)
                {
                    foreach (var error in state.Value.Errors)
                    {
                        _logger.LogError($"Error en {state.Key}: {error.ErrorMessage}");
                    }
                }
            }
        }

        // AJAX: Obtener tallas por producto
        [HttpGet]
        public async Task<IActionResult> GetTallasByProducto(int productoId)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Json(new List<object>());
                }

                var tallas = await _context.TallasInventario
                    .Include(ti => ti.Inventario)
                    .Where(ti => ti.InventarioId == productoId &&
                                ti.Cantidad > 0 &&
                                ti.Inventario.UsuarioId == currentUser.Id)
                    .Select(ti => new
                    {
                        id = ti.Id,
                        talla = ti.Talla,
                        cantidad = ti.Cantidad
                    })
                    .ToListAsync();

                return Json(tallas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GetTallasByProducto");
                return Json(new List<object>());
            }
        }

        // POST: Prestamo/Devolver/5
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Devolver(int id)
{
    var currentUser = await _userManager.GetUserAsync(User);

    try
    {
        var prestamo = await _context.Prestamos
            .Include(p => p.TallaInventario)
                .ThenInclude(ti => ti.Inventario)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (prestamo == null)
        {
            TempData["ErrorMessage"] = "Préstamo no encontrado";
            return RedirectToAction(nameof(Index));
        }

        // Verificar permisos
        if (prestamo.UsuarioPrestamistaId != currentUser.Id && prestamo.UsuarioReceptorId != currentUser.Id)
        {
            TempData["ErrorMessage"] = "No tienes permiso para devolver este préstamo";
            return RedirectToAction(nameof(Index));
        }

        if (prestamo.Estado != "Prestado")
        {
            TempData["WarningMessage"] = $"Este préstamo ya ha sido {prestamo.Estado.ToLower()}";
            return RedirectToAction(nameof(Index));
        }

        bool esPrestamista = prestamo.UsuarioPrestamistaId == currentUser.Id;

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            prestamo.Estado = "Devuelto";
            _context.Update(prestamo);

            if (esPrestamista)
            {
                // Devolver al inventario del prestamista
                prestamo.TallaInventario.Cantidad += prestamo.Cantidad;
                _context.Update(prestamo.TallaInventario);
            }
            else
            {
                // Receptor devolviendo - agregar al inventario del receptor
                var nombreProducto = prestamo.TallaInventario.Inventario.Nombre;
                var generoProducto = prestamo.TallaInventario.Inventario.Genero;
                var tallaProducto = prestamo.TallaInventario.Talla;

                // Buscar o crear el producto en el inventario del receptor
                var inventarioReceptor = await _context.Inventarios
                    .FirstOrDefaultAsync(i => i.UsuarioId == currentUser.Id &&
                                           i.Nombre == nombreProducto &&
                                           i.Genero == generoProducto);

                if (inventarioReceptor == null)
                {
                    inventarioReceptor = new Inventario
                    {
                        Nombre = nombreProducto,
                        Genero = generoProducto,
                        UsuarioId = currentUser.Id,
                        Costo = 0, // No sabemos el costo original
                        PrecioVenta = prestamo.TallaInventario.Inventario.PrecioVenta
                    };
                    _context.Add(inventarioReceptor);
                    await _context.SaveChangesAsync(); // Guardar para obtener el ID
                }

                // Buscar o crear la talla en el inventario del receptor
                var tallaReceptor = await _context.TallasInventario
                    .FirstOrDefaultAsync(t => t.InventarioId == inventarioReceptor.Id &&
                                           t.Talla == tallaProducto);

                if (tallaReceptor == null)
                {
                    tallaReceptor = new TallaInventario
                    {
                        InventarioId = inventarioReceptor.Id,
                        Talla = tallaProducto,
                        Cantidad = prestamo.Cantidad
                    };
                    _context.Add(tallaReceptor);
                }
                else
                {
                    tallaReceptor.Cantidad += prestamo.Cantidad;
                    _context.Update(tallaReceptor);
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["SuccessMessage"] = "Préstamo devuelto exitosamente";
            return RedirectToAction(nameof(Index));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error al devolver préstamo");
        TempData["ErrorMessage"] = "Error al devolver el préstamo";
        return RedirectToAction(nameof(Index));
    }
}

        // GET: Prestamo/Devolver/5
        public async Task<IActionResult> Devolver(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var prestamo = await _context.Prestamos
                .Include(p => p.TallaInventario)
                    .ThenInclude(ti => ti.Inventario)
                .Include(p => p.UsuarioPrestamista)
                .Include(p => p.UsuarioReceptor)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (prestamo == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (prestamo.UsuarioPrestamistaId != currentUser.Id && prestamo.UsuarioReceptorId != currentUser.Id)
            {
                return Forbid();
            }

            // Determinar si es un préstamo realizado o recibido
            prestamo.TipoPrestamo = prestamo.UsuarioPrestamistaId == currentUser.Id ? "Realizado" : "Recibido";
            prestamo.LocalPersona = prestamo.TipoPrestamo == "Realizado"
                ? prestamo.UsuarioReceptor?.UserName
                : prestamo.UsuarioPrestamista?.UserName;

            return View(prestamo);
        }


        // AJAX: Obtener cantidad disponible
        [HttpGet]
        public async Task<IActionResult> GetCantidadDisponible(int tallaInventarioId)
        {
            try
            {
                var talla = await _context.TallasInventario
                    .FirstOrDefaultAsync(ti => ti.Id == tallaInventarioId);

                if (talla == null)
                {
                    return Json(new { cantidad = 0 });
                }

                return Json(new { cantidad = talla.Cantidad });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GetCantidadDisponible");
                return Json(new { cantidad = 0 });
            }
        }



        // Método de prueba para verificar que el controlador responde
        [HttpGet]
        public IActionResult Test()
        {
            _logger.LogInformation("Método Test llamado - el controlador responde correctamente");
            return Json(new { mensaje = "Controlador funcionando", timestamp = DateTime.Now });
        }

        // Método auxiliar mejorado
        private async Task LoadCreateViewData(IdentityUser currentUser)
        {
            try
            {
                var productos = await _context.Inventarios
                    .Where(i => i.UsuarioId == currentUser.Id)
                    .Select(i => new ProductoSelectViewModel { Id = i.Id, Nombre = i.Nombre })
                    .ToListAsync();

                var usuarios = await _userManager.Users
                    .Where(u => u.Id != currentUser.Id)
                    .Select(u => new UsuarioSelectViewModel { Id = u.Id, UserName = u.UserName })
                    .ToListAsync();

                ViewBag.Productos = productos ?? new List<ProductoSelectViewModel>();
                ViewBag.Usuarios = usuarios ?? new List<UsuarioSelectViewModel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar datos para la vista Create");
                ViewBag.Productos = new List<ProductoSelectViewModel>();
                ViewBag.Usuarios = new List<UsuarioSelectViewModel>();
            }
        }

        // Método para obtener tallas de un producto (AJAX)


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarComoVendido(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);

            try
            {
                var prestamo = await _context.Prestamos
                    .Include(p => p.TallaInventario)
                        .ThenInclude(ti => ti.Inventario)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (prestamo == null)
                {
                    TempData["ErrorMessage"] = "Préstamo no encontrado";
                    return RedirectToAction(nameof(Index));
                }

                // Verificar permisos
                if (prestamo.UsuarioPrestamistaId != currentUser.Id && prestamo.UsuarioReceptorId != currentUser.Id)
                {
                    TempData["ErrorMessage"] = "No tienes permiso para esta acción";
                    return RedirectToAction(nameof(Index));
                }

                // Verificar que el préstamo no esté ya devuelto o vendido
                if (prestamo.Estado != "Prestado")
                {
                    TempData["WarningMessage"] = $"Este préstamo ya ha sido {prestamo.Estado.ToLower()}";
                    return RedirectToAction(nameof(Index));
                }

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Marcar como vendido (no devolvemos al inventario)
                    prestamo.Estado = "Vendido";
                    _context.Update(prestamo);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["SuccessMessage"] = "Préstamo marcado como vendido exitosamente";
                    return RedirectToAction(nameof(Index));
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al marcar préstamo como vendido");
                TempData["ErrorMessage"] = "Error al marcar el préstamo como vendido";
                return RedirectToAction(nameof(Index));
            }
        }

    }
}