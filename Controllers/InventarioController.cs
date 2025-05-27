using Microsoft.AspNetCore.Mvc;
using Tenis3t.Models;
using Microsoft.EntityFrameworkCore;
using Tenis3t.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Tenis3t.Controllers
{
    [Authorize]
    public class InventarioController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<InventarioController> _logger;

        public InventarioController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            ILogger<InventarioController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string nombre = null)
        {
            var usuarioActual = await _userManager.GetUserAsync(User);

            var inventarios = _context.Inventarios
                .Include(i => i.Tallas) // ¡Esta línea es crucial!
                .Where(i => i.UsuarioId == usuarioActual.Id);

            if (!string.IsNullOrEmpty(nombre))
            {
                inventarios = inventarios.Where(i => i.Nombre.Contains(nombre));
            }

            return View(await inventarios.ToListAsync());
        }

        [HttpGet]
        public IActionResult BuscarProducto(string nombre)
        {
            var usuarioActualId = _userManager.GetUserId(User);

            var inventario = _context.Inventarios
                .Where(i => i.UsuarioId == usuarioActualId &&
                           i.Nombre.ToLower().Contains(nombre.ToLower()))
                .OrderBy(i => i.Nombre)
                .Select(i => new
                {
                    nombre = i.Nombre,
                    cantidad = i.Cantidad,
                    precioVenta = i.PrecioVenta
                })
                .FirstOrDefault();

            return Json(inventario);
        }

        public IActionResult Create()
        {
            ViewBag.Generos = new List<SelectListItem>
        {
            new SelectListItem { Value = "hombre", Text = "Hombre" },
            new SelectListItem { Value = "dama", Text = "Dama" }
        };

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Inventario inventario, Dictionary<string, int> tallas)
        {
            var usuarioActual = await _userManager.GetUserAsync(User);
            inventario.UsuarioId = usuarioActual.Id;

            ModelState.Remove("Usuario");
            ModelState.Remove("UsuarioId");
            ModelState.Remove("Tallas");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(inventario);
                    await _context.SaveChangesAsync();

                    // Procesar las tallas
                    if (tallas != null)
                    {
                        foreach (var talla in tallas)
                        {
                            if (talla.Value > 0) // Solo agregar tallas con cantidad > 0
                            {
                                _context.TallasInventario.Add(new TallaInventario
                                {
                                    InventarioId = inventario.Id,
                                    Talla = talla.Key,
                                    Cantidad = talla.Value
                                });
                            }
                        }
                        await _context.SaveChangesAsync();
                    }

                    TempData["SuccessMessage"] = "Producto agregado correctamente";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al guardar producto");
                    ModelState.AddModelError("", "Error al guardar el producto: " + ex.Message);
                    TempData["ErrorMessage"] = "Ocurrió un error al guardar el producto";
                }
            }

            ViewBag.Generos = new List<SelectListItem>
        {
            new SelectListItem { Value = "hombre", Text = "Hombre" },
            new SelectListItem { Value = "dama", Text = "Dama" }
        };

            return View(inventario);
        }
        // Los demás métodos (Edit, Delete, etc.) deben incluir validación de usuario
        public async Task<IActionResult> Edit(int id)
        {
            var usuarioActualId = _userManager.GetUserId(User);
            var inventario = await _context.Inventarios
                .FirstOrDefaultAsync(i => i.Id == id && i.UsuarioId == usuarioActualId);

            if (inventario == null)
            {
                return NotFound();
            }
            return View(inventario);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Inventario inventario, Dictionary<string, int> tallas)
        {
            var usuarioActualId = _userManager.GetUserId(User);

            if (id != inventario.Id)
            {
                return NotFound();
            }

            // Verificar que el inventario pertenece al usuario
            var inventarioExistente = await _context.Inventarios
                .Include(i => i.Tallas)
                .FirstOrDefaultAsync(i => i.Id == id && i.UsuarioId == usuarioActualId);

            if (inventarioExistente == null)
            {
                return NotFound();
            }

            ModelState.Remove("Usuario");
            ModelState.Remove("UsuarioId");
            ModelState.Remove("Tallas");

            if (ModelState.IsValid)
            {
                try
                {
                    // Actualizar propiedades básicas
                    inventarioExistente.Nombre = inventario.Nombre;
                    inventarioExistente.Costo = inventario.Costo;
                    inventarioExistente.PrecioVenta = inventario.PrecioVenta;

                    // Actualizar tallas
                    if (tallas != null)
                    {
                        // Eliminar tallas que ya no existen
                        var tallasAEliminar = inventarioExistente.Tallas
                            .Where(t => !tallas.ContainsKey(t.Talla))
                            .ToList();

                        foreach (var talla in tallasAEliminar)
                        {
                            _context.TallasInventario.Remove(talla);
                        }

                        // Actualizar o agregar tallas
                        foreach (var talla in tallas)
                        {
                            var tallaExistente = inventarioExistente.Tallas
                                .FirstOrDefault(t => t.Talla == talla.Key);

                            if (tallaExistente != null)
                            {
                                tallaExistente.Cantidad = talla.Value;
                            }
                            else if (talla.Value > 0)
                            {
                                _context.TallasInventario.Add(new TallaInventario
                                {
                                    InventarioId = inventarioExistente.Id,
                                    Talla = talla.Key,
                                    Cantidad = talla.Value
                                });
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Producto actualizado correctamente";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    if (!ProductoExists(inventario.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        ModelState.AddModelError("", "Error de concurrencia: " + ex.Message);
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error al actualizar: " + ex.Message);
                }
            }

            // Si hay errores, recargar las tallas para mostrar en la vista
            inventario.Tallas = await _context.TallasInventario
                .Where(t => t.InventarioId == id)
                .ToListAsync();

            return View(inventario);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var usuarioActualId = _userManager.GetUserId(User);
            var inventario = await _context.Inventarios
                .FirstOrDefaultAsync(m => m.Id == id && m.UsuarioId == usuarioActualId);

            if (inventario == null)
            {
                return NotFound();
            }

            return View(inventario);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var usuarioActualId = _userManager.GetUserId(User);
                var inventario = await _context.Inventarios
                    .FirstOrDefaultAsync(m => m.Id == id && m.UsuarioId == usuarioActualId);

                if (inventario == null)
                {
                    TempData["ErrorMessage"] = "Producto no encontrado";
                    return RedirectToAction(nameof(Index));
                }

                _context.Inventarios.Remove(inventario);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Producto eliminado correctamente";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error al eliminar: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            var usuarioActualId = _userManager.GetUserId(User);
            var inventario = await _context.Inventarios
                .FirstOrDefaultAsync(m => m.Id == id && m.UsuarioId == usuarioActualId);

            if (inventario == null)
            {
                return NotFound();
            }

            return View(inventario);
        }

        private bool ProductoExists(int id)
        {
            var usuarioActualId = _userManager.GetUserId(User);
            return _context.Inventarios.Any(e => e.Id == id && e.UsuarioId == usuarioActualId);
        }
    }
}