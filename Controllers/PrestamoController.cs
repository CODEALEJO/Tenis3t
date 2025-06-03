using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tenis3t.Data;
using Tenis3t.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System;
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
        public async Task<IActionResult> Index(string tipo = "todos", string filtroNombre = "", DateTime? filtroFecha = null)
        {
            var usuarioActual = await _userManager.GetUserAsync(User);
            var query = _context.Prestamos
                .Include(p => p.TallaInventario)
                    .ThenInclude(t => t.Inventario)
                .Include(p => p.Usuario)
                .Where(p => p.UsuarioId == usuarioActual.Id);

            if (!string.IsNullOrEmpty(filtroNombre))
            {
                query = query.Where(p => p.LocalPersona.Contains(filtroNombre));
            }

            if (filtroFecha.HasValue)
            {
                query = query.Where(p => p.FechaPrestamo.Date == filtroFecha.Value.Date);
            }

            if (tipo == "realizados")
            {
                query = query.Where(p => p.TipoPrestamo == "Realizado");
            }
            else if (tipo == "recibidos")
            {
                query = query.Where(p => p.TipoPrestamo == "Recibido");
            }

            ViewBag.TipoSeleccionado = tipo;
            ViewBag.FiltroNombre = filtroNombre;
            ViewBag.FiltroFecha = filtroFecha?.ToString("yyyy-MM-dd");

            return View(await query.OrderByDescending(p => p.FechaPrestamo).ToListAsync());
        }

        // GET: Prestamo/Create
        public async Task<IActionResult> Create(string tipo)
        {
            var usuarioActual = await _userManager.GetUserAsync(User);
            var productos = await _context.Inventarios
                .Where(i => i.UsuarioId == usuarioActual.Id)
                .Include(i => i.Tallas)
                .Where(i => i.Tallas.Any(t => t.Cantidad > 0))
                .ToListAsync();

            ViewBag.Productos = productos;
            ViewBag.TipoPrestamo = tipo;
            return View();
        }

        [HttpGet]
        [Authorize]
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

        // POST: Prestamo/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Prestamo prestamo)
        {
            var usuarioActual = await _userManager.GetUserAsync(User);
            prestamo.UsuarioId = usuarioActual.Id;

            ModelState.Remove("Usuario");
            ModelState.Remove("TallaInventario");
            ModelState.Remove("Inventario");

            if (ModelState.IsValid)
            {
                var talla = await _context.TallasInventario
                    .Include(t => t.Inventario)
                    .FirstOrDefaultAsync(t => t.Id == prestamo.TallaInventarioId);

                if (talla == null || talla.Inventario.UsuarioId != usuarioActual.Id)
                {
                    ModelState.AddModelError("TallaInventarioId", "Talla no encontrada o no tienes permisos");
                    await LoadViewBagData(usuarioActual.Id, prestamo.TipoPrestamo);
                    return View(prestamo);
                }

                if (talla.Cantidad < prestamo.Cantidad)
                {
                    ModelState.AddModelError("Cantidad", $"No hay suficiente stock disponible. Stock actual en talla {talla.Talla}: {talla.Cantidad}");
                    await LoadViewBagData(usuarioActual.Id, prestamo.TipoPrestamo);
                    return View(prestamo);
                }

                try
                {
                    // Actualizar la cantidad en la talla específica
                    talla.Cantidad -= prestamo.Cantidad;
                    _context.Update(talla);

                    // Guardar el préstamo
                    prestamo.Estado = "Prestado";
                    _context.Add(prestamo);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Préstamo registrado correctamente";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al crear préstamo");
                    ModelState.AddModelError("", "Ocurrió un error al registrar el préstamo");
                    await LoadViewBagData(usuarioActual.Id, prestamo.TipoPrestamo);
                    return View(prestamo);
                }
            }

            await LoadViewBagData(usuarioActual.Id, prestamo.TipoPrestamo);
            return View(prestamo);
        }

        // GET: Prestamo/Devolver/5
        public async Task<IActionResult> Devolver(int id)
        {
            var usuarioActual = await _userManager.GetUserAsync(User);
            var prestamo = await _context.Prestamos
                .Include(p => p.TallaInventario)
                .Include(p => p.Usuario)
                .FirstOrDefaultAsync(p => p.Id == id && p.UsuarioId == usuarioActual.Id);

            if (prestamo == null)
            {
                return NotFound();
            }

            return View(prestamo);
        }

        // POST: Prestamo/Devolver/5
        [HttpPost, ActionName("Devolver")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DevolverConfirmado(int id)
        {
            var usuarioActual = await _userManager.GetUserAsync(User);
            var prestamo = await _context.Prestamos
                .Include(p => p.TallaInventario)
                .FirstOrDefaultAsync(p => p.Id == id && p.UsuarioId == usuarioActual.Id);

            if (prestamo == null || prestamo.TallaInventario == null)
            {
                return NotFound();
            }

            try
            {
                prestamo.Estado = "Devuelto";
                prestamo.FechaDevolucionReal = DateTime.Now;

                // Devolver el stock a la talla específica
                prestamo.TallaInventario.Cantidad += prestamo.Cantidad;
                _context.Update(prestamo.TallaInventario);

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
    }
}