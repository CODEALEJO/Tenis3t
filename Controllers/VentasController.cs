using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tenis3t.Data;
using Tenis3t.Models;

namespace Tenis3t.Controllers
{
    [Authorize]
    public class VentasController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<VentasController> _logger;

        public VentasController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            ILogger<VentasController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: Ventas
        public async Task<IActionResult> Index()
        {
            var usuarioActual = await _userManager.GetUserAsync(User);
            var ventas = await _context.Ventas
                .Include(v => v.UsuarioVendedor)
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.TallaInventario)
                        .ThenInclude(t => t.Inventario)
                .Where(v => v.UsuarioVendedorId == usuarioActual.Id)
                .OrderByDescending(v => v.FechaVenta)
                .ToListAsync();

            return View(ventas);
        }

        // GET: Ventas/Create
        public async Task<IActionResult> Create()
        {
            var usuarioActual = await _userManager.GetUserAsync(User);
            var inventarioDisponible = await _context.Inventarios
                .Include(i => i.Tallas)
                .Where(i => i.UsuarioId == usuarioActual.Id && i.Tallas.Any(t => t.Cantidad > 0))
                .ToListAsync();

            ViewBag.InventarioDisponible = inventarioDisponible;
            return View(new VentaViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(VentaViewModel model)
        {
            var usuarioActual = await _userManager.GetUserAsync(User);

            if (!ModelState.IsValid)
            {
                var inventarioDisponible = await _context.Inventarios
                    .Include(i => i.Tallas)
                    .Where(i => i.UsuarioId == usuarioActual.Id && i.Tallas.Any(t => t.Cantidad > 0))
                    .ToListAsync();

                ViewBag.InventarioDisponible = inventarioDisponible;
                return View(model);
            }

            // Obtenemos la estrategia de ejecución
            var executionStrategy = _context.Database.CreateExecutionStrategy();

            try
            {
                await executionStrategy.ExecuteAsync(async () =>
                {
                    using (var transaction = await _context.Database.BeginTransactionAsync())
                    {
                        try
                        {
                            // Crear la venta principal
                            var venta = new Venta
                            {
                                FechaVenta = DateTime.Now,
                                Estado = "Completada",
                                UsuarioVendedorId = usuarioActual.Id,
                                Cliente = model.Cliente,
                                Total = 0
                            };

                            _context.Add(venta);
                            await _context.SaveChangesAsync();

                            decimal totalVenta = 0;

                            foreach (var detalle in model.Detalles)
                            {
                                var tallaInventario = await _context.TallasInventario
                                    .Include(t => t.Inventario)
                                    .FirstOrDefaultAsync(t => t.Id == detalle.TallaInventarioId);

                                if (tallaInventario == null)
                                {
                                    throw new Exception($"Talla de inventario no encontrada: {detalle.TallaInventarioId}");
                                }

                                if (tallaInventario.Cantidad < detalle.Cantidad)
                                {
                                    throw new Exception($"No hay suficiente stock para {tallaInventario.Inventario.Nombre} talla {tallaInventario.Talla}. Disponible: {tallaInventario.Cantidad}, Solicitado: {detalle.Cantidad}");
                                }

                                var detalleVenta = new DetalleVenta
                                {
                                    VentaId = venta.Id,
                                    TallaInventarioId = detalle.TallaInventarioId,
                                    Cantidad = detalle.Cantidad,
                                    PrecioUnitario = tallaInventario.Inventario.PrecioVenta
                                };

                                totalVenta += detalleVenta.Subtotal;
                                tallaInventario.Cantidad -= detalleVenta.Cantidad;

                                _context.Add(detalleVenta);
                                _context.Update(tallaInventario);
                            }

                            venta.Total = totalVenta;
                            _context.Update(venta);

                            await _context.SaveChangesAsync();
                            await transaction.CommitAsync();
                        }
                        catch (Exception)
                        {
                            await transaction.RollbackAsync();
                            throw;
                        }
                    }
                });

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear venta");
                ModelState.AddModelError(string.Empty, $"Error al procesar la venta: {ex.Message}");

                var inventarioDisponible = await _context.Inventarios
                    .Include(i => i.Tallas)
                    .Where(i => i.UsuarioId == usuarioActual.Id && i.Tallas.Any(t => t.Cantidad > 0))
                    .ToListAsync();

                ViewBag.InventarioDisponible = inventarioDisponible;
                return View(model);
            }
        }

        // GET: Ventas/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var usuarioActual = await _userManager.GetUserAsync(User);
            var venta = await _context.Ventas
                .Include(v => v.UsuarioVendedor)
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.TallaInventario)
                        .ThenInclude(t => t.Inventario)
                .FirstOrDefaultAsync(m => m.Id == id && m.UsuarioVendedorId == usuarioActual.Id);

            if (venta == null)
            {
                return NotFound();
            }

            return View(venta);
        }

        // GET: Ventas/Cancel/5
        public async Task<IActionResult> Cancel(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var usuarioActual = await _userManager.GetUserAsync(User);
            var venta = await _context.Ventas
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.TallaInventario)
                .FirstOrDefaultAsync(v => v.Id == id && v.UsuarioVendedorId == usuarioActual.Id && v.Estado == "Completada");

            if (venta == null)
            {
                return NotFound();
            }

            return View(venta);
        }

        // POST: Ventas/Cancel/5
        [HttpPost, ActionName("Cancel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelConfirmed(int id)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var usuarioActual = await _userManager.GetUserAsync(User);
                    var venta = await _context.Ventas
                        .Include(v => v.Detalles)
                            .ThenInclude(d => d.TallaInventario)
                        .FirstOrDefaultAsync(v => v.Id == id && v.UsuarioVendedorId == usuarioActual.Id && v.Estado == "Completada");

                    if (venta == null)
                    {
                        return NotFound();
                    }

                    // Revertir el inventario para cada detalle
                    foreach (var detalle in venta.Detalles)
                    {
                        detalle.TallaInventario.Cantidad += detalle.Cantidad;
                        _context.Update(detalle.TallaInventario);
                    }

                    // Marcar la venta como cancelada
                    venta.Estado = "Cancelada";
                    _context.Update(venta);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError(string.Empty, $"Error al cancelar la venta: {ex.Message}");
                    _logger.LogError(ex, "Error al cancelar venta");
                    return View("Error");
                }
            }
        }
    }
}