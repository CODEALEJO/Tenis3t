using Microsoft.AspNetCore.Mvc;
using Tenis3t.Models;
using Microsoft.EntityFrameworkCore;
using Tenis3t.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text.Json;

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

        public async Task<IActionResult> Index(string nombre = null, string genero = null)
        {
            var usuarioActual = await _userManager.GetUserAsync(User);

            var inventarios = _context.Inventarios
                .Include(i => i.Tallas)
                .Include(i => i.Salidas)
                .Where(i => i.UsuarioId == usuarioActual.Id);


            // Aplicar filtro por nombre si existe
            if (!string.IsNullOrEmpty(nombre))
            {
                inventarios = inventarios.Where(i => i.Nombre.Contains(nombre));
            }

            // Aplicar filtro por género si existe y no es "todos"
            if (!string.IsNullOrEmpty(genero) && genero != "todos")
            {
                inventarios = inventarios.Where(i => i.Genero == genero);
            }

            // ORDENAR POR NOMBRE DE FORMA ASCENDENTE (A-Z)
            inventarios = inventarios.OrderBy(i => i.Nombre);

            // Pasar los filtros actuales a la vista
            ViewBag.FiltroNombre = nombre;
            ViewBag.FiltroGenero = genero;
            return View(await inventarios.ToListAsync());
        }

        // Nueva acción para imprimir
        public async Task<IActionResult> Imprimir(int id)
        {
            var usuarioActualId = _userManager.GetUserId(User);
            var inventario = await _context.Inventarios
                .Include(i => i.Tallas)
                .FirstOrDefaultAsync(m => m.Id == id && m.UsuarioId == usuarioActualId);

            if (inventario == null)
            {
                return NotFound();
            }

            // Configurar la vista para impresión
            return View("ImprimirProducto", inventario);
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
                    // Verificar si ya existe un producto con el mismo nombre y género
                    var productoExistente = await _context.Inventarios
                        .Include(i => i.Tallas)
                        .FirstOrDefaultAsync(i =>
                            i.UsuarioId == usuarioActual.Id &&
                            i.Nombre.ToLower() == inventario.Nombre.ToLower() &&
                            i.Genero == inventario.Genero);

                    if (productoExistente != null)
                    {
                        // Actualizar el producto existente
                        productoExistente.Costo = inventario.Costo;
                        productoExistente.PrecioVenta = inventario.PrecioVenta;

                        // Actualizar tallas
                        if (tallas != null)
                        {
                            foreach (var talla in tallas)
                            {
                                if (talla.Value > 0)
                                {
                                    var tallaExistente = productoExistente.Tallas
                                        .FirstOrDefault(t => t.Talla == talla.Key);

                                    if (tallaExistente != null)
                                    {
                                        tallaExistente.Cantidad += talla.Value;
                                    }
                                    else
                                    {
                                        _context.TallasInventario.Add(new TallaInventario
                                        {
                                            InventarioId = productoExistente.Id,
                                            Talla = talla.Key,
                                            Cantidad = talla.Value
                                        });
                                    }
                                }
                            }
                        }

                        await _context.SaveChangesAsync();
                        TempData["SuccessMessage"] = "Se actualizó el producto existente con las nuevas cantidades";
                        return RedirectToAction(nameof(Index));
                    }

                    // Si no existe, crear nuevo producto
                    _context.Add(inventario);
                    await _context.SaveChangesAsync();

                    // Procesar las tallas
                    if (tallas != null)
                    {
                        foreach (var talla in tallas)
                        {
                            if (talla.Value > 0)
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
            // _logger.LogInformation($"Clave recibida: {claveSeguridad}, Esperada: {DeletePassword}");
            _logger.LogInformation($"ModelState válido: {ModelState.IsValid}");
            if (!ModelState.IsValid)
            {
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogError($"Error de validación: {error.ErrorMessage}");
                }
            }

            return View(inventario);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var usuarioActualId = _userManager.GetUserId(User);
            var inventario = await _context.Inventarios
                .Include(i => i.Tallas)
                .FirstOrDefaultAsync(i => i.Id == id && i.UsuarioId == usuarioActualId);

            if (inventario == null)
            {
                return NotFound();
            }

            // Preparar las tallas para la vista
            var tallasDisponibles = inventario.Genero == "hombre" ?
                new[] { "38", "39", "40", "41", "42", "43", "44", "45", "46", "47", "48", "49", "50" } :
                new[] { "34", "35", "36", "37", "38", "39", "40", "41", "42", "43" };

            ViewBag.TallasDisponibles = tallasDisponibles;
            ViewBag.Generos = new List<SelectListItem>
    {
        new SelectListItem { Value = "hombre", Text = "Hombre", Selected = inventario.Genero == "hombre" },
        new SelectListItem { Value = "dama", Text = "Dama", Selected = inventario.Genero == "dama" }
    };
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

            // Obtener el inventario existente
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
            ModelState.Remove("Salidas");


            _logger.LogInformation($"📥 Entró a Edit POST. Id: {id}, ModelState válido: {ModelState.IsValid}");

            // 🔍 Registrar todos los errores si el ModelState es inválido
            if (!ModelState.IsValid)
            {
                foreach (var kvp in ModelState)
                {
                    foreach (var error in kvp.Value.Errors)
                    {
                        _logger.LogError($"❌ Error en campo {kvp.Key}: {error.ErrorMessage}");
                    }
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Si cambió el género, eliminar todas las tallas existentes
                    if (inventarioExistente.Genero != inventario.Genero)
                    {
                        var tallasAEliminar = inventarioExistente.Tallas.ToList();
                        foreach (var talla in tallasAEliminar)
                        {
                            _context.TallasInventario.Remove(talla);
                        }
                    }

                    // Actualizar propiedades básicas
                    inventarioExistente.Nombre = inventario.Nombre;
                    inventarioExistente.Genero = inventario.Genero;

                    // ✅ Siempre actualiza costo y precio
                    inventarioExistente.Costo = inventario.Costo;
                    inventarioExistente.PrecioVenta = inventario.PrecioVenta;

                    // Procesar tallas
                    if (tallas != null)
                    {
                        // Si no cambió el género, eliminar solo las tallas que no están en el nuevo conjunto
                        if (inventarioExistente.Genero == inventario.Genero)
                        {
                            var tallasAEliminar = inventarioExistente.Tallas
                                .Where(t => !tallas.ContainsKey(t.Talla))
                                .ToList();

                            foreach (var talla in tallasAEliminar)
                            {
                                _context.TallasInventario.Remove(talla);
                            }
                        }

                        // Actualizar o agregar tallas
                        foreach (var talla in tallas)
                        {
                            if (talla.Value >= 0)
                            {
                                var tallaExistente = inventarioExistente.Tallas
                                    .FirstOrDefault(t => t.Talla == talla.Key);

                                if (tallaExistente != null)
                                {
                                    tallaExistente.Cantidad = talla.Value;
                                }
                                else
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

            return await RecargarDatosParaVista(id, inventario);
        }



        // Método auxiliar para recargar datos
        private async Task<IActionResult> RecargarDatosParaVista(int id, Inventario inventarioForm)
        {
            var usuarioActualId = _userManager.GetUserId(User);
            var inventarioBD = await _context.Inventarios
                .Include(i => i.Tallas)
                .FirstOrDefaultAsync(i => i.Id == id && i.UsuarioId == usuarioActualId);

            if (inventarioBD == null)
            {
                return NotFound();
            }

            // Usamos siempre los valores de BD como base
            var inventario = inventarioBD;

            // Si el usuario alcanzó a editar campos antes del error, puedes sobrescribirlos aquí:
            if (!string.IsNullOrEmpty(inventarioForm.Nombre))
                inventario.Nombre = inventarioForm.Nombre;
            if (!string.IsNullOrEmpty(inventarioForm.Genero))
                inventario.Genero = inventarioForm.Genero;
            if (inventarioForm.Costo > 0)
                inventario.Costo = inventarioForm.Costo;
            if (inventarioForm.PrecioVenta > 0)
                inventario.PrecioVenta = inventarioForm.PrecioVenta;

            // Preparar tallas
            var tallasDisponibles = inventario.Genero == "hombre" ?
                new[] { "38", "39", "40", "41", "42", "43", "44", "45", "46", "47", "48", "49", "50" } :
                new[] { "34", "35", "36", "37", "38", "39", "40", "41", "42", "43" };

            ViewBag.TallasDisponibles = tallasDisponibles;
            ViewBag.Generos = new List<SelectListItem>
    {
        new SelectListItem { Value = "hombre", Text = "Hombre", Selected = inventario.Genero == "hombre" },
        new SelectListItem { Value = "dama", Text = "Dama", Selected = inventario.Genero == "dama" }
    };

            return View("Edit", inventario);
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
                .Include(i => i.Tallas)
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


        // GET: Inventario/CreateMultiple
        public IActionResult CreateMultiple()
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
        public async Task<IActionResult> CreateMultiple(List<Inventario> inventarios, List<string> tallas)
        {
            var usuarioActual = await _userManager.GetUserAsync(User);
            var loteId = Guid.NewGuid().ToString();

            if (inventarios != null && inventarios.Count > 0)
            {
                for (int i = 0; i < inventarios.Count; i++)
                {
                    var inventario = inventarios[i];
                    inventario.UsuarioId = usuarioActual.Id;
                    inventario.LoteIngreso = loteId; // 🔹 Asigna el mismo lote a todos
                    inventario.FechaIngreso = DateTime.Now;

                    ModelState.Remove("Usuario");
                    ModelState.Remove("UsuarioId");
                    ModelState.Remove("Tallas");

                    if (!string.IsNullOrEmpty(inventario.Nombre))
                    {
                        // Buscar si ya existe producto con el mismo Nombre y Genero
                        var existente = await _context.Inventarios
                            .FirstOrDefaultAsync(x => x.Nombre == inventario.Nombre && x.Genero == inventario.Genero);

                        if (existente != null)
                        {
                            // 🔹 Asegurar que el producto quede dentro del mismo lote
                            existente.LoteIngreso = loteId;
                            existente.FechaIngreso = DateTime.Now;

                            // 🔹 Actualizar precios si cambiaron
                            if (existente.Costo != inventario.Costo)
                                existente.Costo = inventario.Costo;

                            if (existente.PrecioVenta != inventario.PrecioVenta)
                                existente.PrecioVenta = inventario.PrecioVenta;

                            _context.Update(existente);
                            await _context.SaveChangesAsync();

                            // 🔹 Procesar tallas nuevas
                            if (tallas != null && tallas.Count > i && !string.IsNullOrEmpty(tallas[i]))
                            {
                                try
                                {
                                    var tallasDict = JsonSerializer.Deserialize<Dictionary<string, int>>(tallas[i]);
                                    if (tallasDict != null)
                                    {
                                        foreach (var talla in tallasDict)
                                        {
                                            if (talla.Value > 0)
                                            {
                                                var tallaExistente = await _context.TallasInventario
                                                    .FirstOrDefaultAsync(t => t.InventarioId == existente.Id && t.Talla == talla.Key);

                                                if (tallaExistente != null)
                                                {
                                                    tallaExistente.Cantidad += talla.Value;
                                                    _context.Update(tallaExistente);
                                                }
                                                else
                                                {
                                                    _context.TallasInventario.Add(new TallaInventario
                                                    {
                                                        InventarioId = existente.Id,
                                                        Talla = talla.Key,
                                                        Cantidad = talla.Value
                                                    });
                                                }
                                            }
                                        }
                                        await _context.SaveChangesAsync();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error deserializando tallas para producto {Producto}", inventario.Nombre);
                                }
                            }
                        }
                        else
                        {
                            // 🔹 Si no existe -> crear nuevo producto
                            _context.Add(inventario);
                            await _context.SaveChangesAsync();

                            // Procesar tallas para el nuevo producto
                            if (tallas != null && tallas.Count > i && !string.IsNullOrEmpty(tallas[i]))
                            {
                                try
                                {
                                    var tallasDict = JsonSerializer.Deserialize<Dictionary<string, int>>(tallas[i]);
                                    if (tallasDict != null)
                                    {
                                        foreach (var talla in tallasDict)
                                        {
                                            if (talla.Value > 0)
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
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error deserializando tallas para producto {Producto}", inventario.Nombre);
                                }
                            }
                        }
                    }
                }

                TempData["SuccessMessage"] = "Productos cargados correctamente";
                return RedirectToAction(nameof(Index));
            }

            TempData["ErrorMessage"] = "No se recibieron productos válidos";
            return View();
        }

        public async Task<IActionResult> HistorialIngreso()
        {
            var usuarioActual = await _userManager.GetUserAsync(User);
            if (usuarioActual == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var ingresos = _context.Inventarios
                .Include(i => i.Tallas)
                .Where(i => i.UsuarioId == usuarioActual.Id) // 👈 FILTRA POR USUARIO ACTUAL
                .GroupBy(i => i.LoteIngreso)
                .Select(g => new IngresoViewModel
                {
                    LoteIngreso = g.Key,
                    FechaIngreso = g.First().FechaIngreso,
                    CantidadProductos = g.Count(),
                    Productos = g.Select(p => new ProductoViewModel
                    {
                        Nombre = p.Nombre,
                        Genero = p.Genero,
                        Costo = p.Costo,
                        PrecioVenta = p.PrecioVenta,
                        Tallas = p.Tallas.Select(t => new TallaViewModel
                        {
                            Talla = t.Talla,
                            Cantidad = t.Cantidad
                        }).ToList()
                    }).ToList()
                })
                .OrderByDescending(i => i.FechaIngreso)
                .ToList();

            return View(ingresos);
        }


    }
}