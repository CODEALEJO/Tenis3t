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
using Tenis3t.Migrations;

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
                    query = query.Where(p => p.UsuarioPrestamistaId == currentUser.Id ||
                                             p.UsuarioReceptorId == currentUser.Id);
                    break;
            }

            // Traer datos
            var prestamos = await query.ToListAsync();

            // Ordenar en memoria: primero Prestado, luego Vendido, luego otros,
            // y dentro de cada grupo por nombre alfabético
            prestamos = prestamos
                .OrderBy(p => p.Estado?.Trim().ToLower() == "prestado" ? 0 :
                              p.Estado?.Trim().ToLower() == "vendido" ? 1 : 2)
                .ThenBy(p => p.TallaInventario.Inventario.Nombre)
                .ToList();

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

        [HttpGet]
        public async Task<IActionResult> BuscarProductos(string termino)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized();

            var productos = await _context.Inventarios
                .Where(i => i.UsuarioId == currentUser.Id &&
                            i.Nombre.Contains(termino) &&
                            i.Tallas.Any(t => t.Cantidad > 0)) // solo productos con stock
                .OrderBy(i => i.Nombre)
                .Select(i => new
                {
                    id = i.Id,
                    nombre = i.Nombre
                })
                .Take(10)
                .ToListAsync();

            return Json(productos);
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




        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarComoVendido(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var executionStrategy = _context.Database.CreateExecutionStrategy();

            return await executionStrategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

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

                    if (prestamo.UsuarioPrestamistaId != currentUser.Id)
                    {
                        TempData["ErrorMessage"] = "No tienes permiso para esta acción";
                        return RedirectToAction(nameof(Index));
                    }

                    if (prestamo.Estado != "Prestado")
                    {
                        TempData["WarningMessage"] = $"Este préstamo ya ha sido {prestamo.Estado.ToLower()}";
                        return RedirectToAction(nameof(Index));
                    }

                    prestamo.Estado = "Vendido";
                    prestamo.FechaDevolucion = DateTime.Now;
                    _context.Update(prestamo);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["SuccessMessage"] = "Préstamo marcado como vendido exitosamente";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error al marcar préstamo como vendido");
                    TempData["ErrorMessage"] = "Error al marcar el préstamo como vendido";
                    return RedirectToAction(nameof(Index));
                }
            });
        }


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

                if (prestamo.Estado != "Prestado")
                {
                    TempData["WarningMessage"] = $"Este préstamo ya ha sido {prestamo.Estado.ToLower()}";
                    return RedirectToAction(nameof(Index));
                }

                if (prestamo.UsuarioReceptorId != currentUser.Id)
                {
                    TempData["ErrorMessage"] = "Solo el receptor puede devolver el préstamo";
                    return RedirectToAction(nameof(Index));
                }

                // Usar la estrategia de ejecución correctamente
                var executionStrategy = _context.Database.CreateExecutionStrategy();

                await executionStrategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        // Devolver la cantidad al inventario
                        prestamo.TallaInventario.Cantidad += prestamo.Cantidad;

                        // Marcar como devuelto
                        prestamo.Estado = "Devuelto";
                        prestamo.FechaDevolucion = DateTime.Now;

                        _context.Update(prestamo);
                        _context.Update(prestamo.TallaInventario);
                        await _context.SaveChangesAsync();

                        await transaction.CommitAsync();

                        TempData["SuccessMessage"] = $"Se han devuelto {prestamo.Cantidad} unidades de {prestamo.TallaInventario.Inventario.Nombre}";
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Error al procesar la devolución del préstamo {PrestamoId}", id);
                        throw;
                    }
                });

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al intentar devolver el préstamo {PrestamoId}", id);
                TempData["ErrorMessage"] = "Ocurrió un error al intentar devolver el préstamo";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}