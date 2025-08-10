using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tenis3t.Data;
using Tenis3t.Models;
using Tenis3t.Extensions;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Globalization;

namespace Tenis3t.Controllers
{
    [Authorize]
    public class VentasController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<VentasController> _logger;
        private const string DeletePassword = "3T2025";

        public VentasController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            ILogger<VentasController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IActionResult> Index(
     string nombreCliente = null,
     string fechaFiltro = null,
     int? mesFiltro = null,
     int? anioFiltro = null,
     string tipoFiltro = "dia")
        {
            var usuarioActual = await _userManager.GetUserAsync(User);
            if (usuarioActual == null)
            {
                return Unauthorized();
            }

            // Consulta base con includes - QUITA EL INCLUDE DE NombreCliente
            var query = _context.Ventas
                .Include(v => v.UsuarioVendedor)
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.TallaInventario)
                        .ThenInclude(t => t.Inventario)
                .Include(v => v.Pagos)
                    .ThenInclude(p => p.MetodoPago)
                .Where(v => v.UsuarioVendedorId == usuarioActual.Id)
                .OrderByDescending(v => v.FechaVenta)
                .AsQueryable();

            // Filtro por nombre de cliente (esto sigue siendo válido)
            if (!string.IsNullOrEmpty(nombreCliente))
            {
                query = query.Where(v => v.NombreCliente != null && v.NombreCliente.Contains(nombreCliente));
            }

            // Aplicar filtros por fecha según el tipo seleccionado
            switch (tipoFiltro.ToLower())
            {
                case "dia":
                    if (!string.IsNullOrEmpty(fechaFiltro) && DateTime.TryParse(fechaFiltro, out var fecha))
                    {
                        var fechaInicio = fecha.Date;
                        var fechaFin = fecha.Date.AddDays(1).AddTicks(-1);
                        query = query.Where(v => v.FechaVenta >= fechaInicio && v.FechaVenta <= fechaFin);
                    }
                    break;

                case "mes":
                    if (mesFiltro.HasValue && mesFiltro > 0 && mesFiltro <= 12 && anioFiltro.HasValue)
                    {
                        var primerDiaMes = new DateTime(anioFiltro.Value, mesFiltro.Value, 1);
                        var ultimoDiaMes = primerDiaMes.AddMonths(1).AddDays(-1);
                        query = query.Where(v => v.FechaVenta >= primerDiaMes && v.FechaVenta <= ultimoDiaMes);
                    }
                    break;

                case "rango":
                    if (!string.IsNullOrEmpty(fechaFiltro))
                    {
                        // Simplificar el procesamiento del rango
                        var partes = fechaFiltro.Split(new[] { " a " }, StringSplitOptions.RemoveEmptyEntries);

                        if (partes.Length == 2)
                        {
                            var cultura = CultureInfo.CreateSpecificCulture("es-CO");
                            var formatos = new[] { "dd/MM/yyyy", "dd-MM-yyyy", "yyyy-MM-dd", "MM/dd/yyyy" };

                            if (DateTime.TryParseExact(partes[0].Trim(), formatos, cultura, DateTimeStyles.None, out var fechaInicio) &&
                                DateTime.TryParseExact(partes[1].Trim(), formatos, cultura, DateTimeStyles.None, out var fechaFin))
                            {
                                // Asegurar que fechaInicio es menor o igual a fechaFin
                                if (fechaInicio > fechaFin)
                                {
                                    (fechaFin, fechaInicio) = (fechaInicio, fechaFin); // Swap values
                                }

                                // Ajustar fechaFin para incluir todo el día
                                fechaFin = fechaFin.Date.AddDays(1).AddTicks(-1);

                                query = query.Where(v => v.FechaVenta >= fechaInicio && v.FechaVenta <= fechaFin);
                            }
                            else
                            {
                                TempData["ErrorMessage"] = "Formato de fecha no válido. Use DD/MM/YYYY a DD/MM/YYYY";
                            }
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Formato de rango no válido. Use DD/MM/YYYY a DD/MM/YYYY";
                        }
                    }
                    break;
            }

            // Ordenar y ejecutar la consulta
            var ventas = await query.OrderByDescending(v => v.FechaVenta).ToListAsync();

            // Obtener años disponibles para el dropdown
            var añosDisponibles = await _context.Ventas
                .Where(v => v.UsuarioVendedorId == usuarioActual.Id)
                .Select(v => v.FechaVenta.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();

            // Pasar datos a la vista
            ViewBag.NombreCliente = nombreCliente;
            ViewBag.FechaFiltro = fechaFiltro;
            ViewBag.MesFiltro = mesFiltro;
            ViewBag.AnioFiltro = anioFiltro;
            ViewBag.TipoFiltro = tipoFiltro;
            ViewBag.AñosDisponibles = añosDisponibles;

            return View(ventas);
        }

        public async Task<IActionResult> Create()
        {
            var usuarioActual = await _userManager.GetUserAsync(User);
            ViewBag.MetodosPago = await _context.MetodoPagos.ToListAsync();

            var productosDisponibles = await _context.Inventarios
                .Include(i => i.Tallas)
                .Where(i => i.UsuarioId == usuarioActual.Id && i.Tallas.Any(t => t.Cantidad > 0))
                .Select(i => new ProductoDisponibleViewModel
                {
                    Id = i.Id,
                    Nombre = i.Nombre,
                    PrecioVenta = i.PrecioVenta,
                    Tallas = i.Tallas.Where(t => t.Cantidad > 0)
                                    .Select(t => new TallaViewModel
                                    {
                                        Id = t.Id,
                                        Talla = t.Talla,
                                        Cantidad = t.Cantidad
                                    }).ToList()
                })
                .ToListAsync();

            ViewBag.ProductosDisponibles = productosDisponibles;

            return View("CreateEdit", new Venta
            {
                FechaVenta = DateTime.Now,
                Descuento = 0,
                Detalles = new List<DetalleVenta>(),
                Pagos = new List<Pago>()
            });
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            ViewBag.MetodosPago = await _context.MetodoPagos.ToListAsync();

            var usuarioActual = await _userManager.GetUserAsync(User);
            var venta = await _context.Ventas
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.TallaInventario)
                        .ThenInclude(t => t.Inventario)
                .Include(v => v.Pagos)
                    .ThenInclude(p => p.MetodoPago)
                .FirstOrDefaultAsync(v => v.Id == id && v.UsuarioVendedorId == usuarioActual.Id);

            if (venta == null) return NotFound();
            if (venta.Estado != "Completada")
            {
                TempData["ErrorMessage"] = "Solo se pueden editar ventas completadas";
                return RedirectToAction(nameof(Index));
            }

            var productosDisponibles = await _context.Inventarios
                .Include(i => i.Tallas)
                .Where(i => i.UsuarioId == usuarioActual.Id)
                .Select(i => new ProductoDisponibleViewModel
                {
                    Id = i.Id,
                    Nombre = i.Nombre,
                    PrecioVenta = i.PrecioVenta,
                    Tallas = i.Tallas.Select(t => new TallaViewModel
                    {
                        Id = t.Id,
                        Talla = t.Talla,
                        Cantidad = t.Cantidad
                    }).ToList()
                })
                .ToListAsync();

            ViewBag.ProductosDisponibles = productosDisponibles;

            return View("CreateEdit", venta);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateEdit(int? id, Venta venta)
        {
            var usuarioActual = await _userManager.GetUserAsync(User);
            bool isEdit = id.HasValue;

            if (isEdit && id != venta.Id) return NotFound();

            // Validaciones básicas (sin cambios)
            if (venta.Detalles == null || !venta.Detalles.Any())
            {
                ModelState.AddModelError("", "Debe agregar al menos un producto");
                await CargarProductosDisponibles(usuarioActual.Id);
                return View("CreateEdit", venta);
            }

            if (venta.Pagos == null || !venta.Pagos.Any(p => p.Monto > 0))
            {
                ModelState.AddModelError("", "Debe agregar al menos un método de pago");
                await CargarProductosDisponibles(usuarioActual.Id);
                return View("CreateEdit", venta);
            }

            // Validar stock y calcular totales (sin cambios)
            decimal subtotal = 0;
            var erroresStock = new List<string>();

            foreach (var detalle in venta.Detalles)
            {
                var talla = await _context.TallasInventario
                    .Include(t => t.Inventario)
                    .FirstOrDefaultAsync(t => t.Id == detalle.TallaInventarioId);

                if (talla == null)
                {
                    erroresStock.Add($"La talla seleccionada ya no existe");
                    continue;
                }
                // Asignar el InventarioId desde la talla
                detalle.InventarioId = talla.Inventario.Id;

                if (isEdit)
                {
                    var detalleOriginal = await _context.DetallesVenta
                        .FirstOrDefaultAsync(d => d.Id == detalle.Id);

                    if (detalleOriginal != null)
                    {
                        var diferencia = detalle.Cantidad - detalleOriginal.Cantidad;
                        if (talla.Cantidad < diferencia)
                        {
                            erroresStock.Add($"No hay suficiente stock para {talla.Inventario.Nombre} - Talla: {talla.Talla}");
                        }
                    }
                }
                else if (talla.Cantidad < detalle.Cantidad)
                {
                    erroresStock.Add($"No hay suficiente stock para {talla.Inventario.Nombre} - Talla: {talla.Talla}");
                }

                detalle.PrecioUnitario = talla.Inventario.PrecioVenta;
                subtotal += detalle.PrecioUnitario * detalle.Cantidad;
            }

            if (erroresStock.Any())
            {
                erroresStock.ForEach(e => ModelState.AddModelError("", e));
                await CargarProductosDisponibles(usuarioActual.Id);
                return View("CreateEdit", venta);
            }

            // Validar métodos de pago (sin cambios)
            decimal totalConDescuento = subtotal * (1 - venta.Descuento / 100m);
            decimal totalPagado = venta.Pagos.Sum(p => p.Monto);

            if (Math.Abs(totalPagado - totalConDescuento) > 0.01m)
            {
                ModelState.AddModelError("",
                    $"La suma de pagos ({totalPagado.ToString("C")}) no coincide con el total ({totalConDescuento.ToString("C")})");
                await CargarProductosDisponibles(usuarioActual.Id);
                return View("CreateEdit", venta);
            }

            // Procesar la venta con manejo de errores mejorado
            try
            {
                var executionStrategy = _context.Database.CreateExecutionStrategy();
                return await executionStrategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        if (isEdit)
                        {
                            await ActualizarVentaExistente(venta);
                        }
                        else
                        {
                            await CrearNuevaVenta(venta, usuarioActual.Id);
                        }

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        TempData["SuccessMessage"] = isEdit ?
                            "Venta actualizada exitosamente!" : "Venta creada exitosamente!";
                        return RedirectToAction(nameof(Index));
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        throw; // Relanzamos la excepción para manejarla fuera
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al {(isEdit ? "editar" : "crear")} venta");
                TempData["ErrorMessage"] = $"Error al procesar la venta: {ex.Message}";
                await CargarProductosDisponibles(usuarioActual.Id);
                return View("CreateEdit", venta);
            }
        }

        private async Task CrearNuevaVenta(Venta venta, string usuarioId)
        {
            venta.FechaVenta = DateTime.Now;
            venta.UsuarioVendedorId = usuarioId;
            venta.Estado = "Completada";
            venta.Total = venta.Detalles.Sum(d => d.PrecioUnitario * d.Cantidad) * (1 - venta.Descuento / 100m);

            // Actualizar stock
            foreach (var detalle in venta.Detalles)
            {
                var talla = await _context.TallasInventario.FindAsync(detalle.TallaInventarioId);
                talla.Cantidad -= detalle.Cantidad;
                _context.Update(talla);
            }

            await _context.Ventas.AddAsync(venta);
        }

        private async Task ActualizarVentaExistente(Venta venta)
        {
            var ventaExistente = await _context.Ventas
                .Include(v => v.Detalles)
                .Include(v => v.Pagos)
                .FirstOrDefaultAsync(v => v.Id == venta.Id);

            if (ventaExistente == null) throw new Exception("Venta no encontrada");

            // Actualizar propiedades básicas
            ventaExistente.NombreCliente = venta.NombreCliente;
            ventaExistente.Descuento = venta.Descuento;
            ventaExistente.Total = venta.Detalles.Sum(d => d.PrecioUnitario * d.Cantidad) * (1 - venta.Descuento / 100m);

            // Actualizar detalles (productos)
            await ActualizarDetallesVenta(ventaExistente, venta.Detalles);

            // Actualizar pagos
            await ActualizarPagosVenta(ventaExistente, venta.Pagos);

            _context.Update(ventaExistente);
        }

        private async Task ActualizarDetallesVenta(Venta ventaExistente, List<DetalleVenta> nuevosDetalles)
        {
            // Eliminar detalles removidos
            var detallesAEliminar = ventaExistente.Detalles
                .Where(d => !nuevosDetalles.Any(nd => nd.Id == d.Id))
                .ToList();

            foreach (var detalle in detallesAEliminar)
            {
                // Devolver stock
                var talla = await _context.TallasInventario.FindAsync(detalle.TallaInventarioId);
                talla.Cantidad += detalle.Cantidad;
                _context.Update(talla);

                _context.DetallesVenta.Remove(detalle);
            }

            // Actualizar/agregar detalles
            foreach (var detalle in nuevosDetalles)
            {
                var detalleExistente = ventaExistente.Detalles.FirstOrDefault(d => d.Id == detalle.Id);
                var talla = await _context.TallasInventario.FindAsync(detalle.TallaInventarioId);

                if (detalleExistente != null)
                {
                    // Ajustar stock según diferencia
                    var diferencia = detalle.Cantidad - detalleExistente.Cantidad;
                    talla.Cantidad -= diferencia;
                    _context.Update(talla);

                    detalleExistente.Cantidad = detalle.Cantidad;
                    detalleExistente.TallaInventarioId = detalle.TallaInventarioId;
                    _context.Update(detalleExistente);
                }
                else
                {
                    // Nuevo detalle
                    talla.Cantidad -= detalle.Cantidad;
                    _context.Update(talla);

                    detalle.VentaId = ventaExistente.Id;
                    await _context.DetallesVenta.AddAsync(detalle);
                }
            }
        }

        private async Task ActualizarPagosVenta(Venta ventaExistente, List<Pago> nuevosPagos)
        {
            // Eliminar pagos removidos
            var pagosAEliminar = ventaExistente.Pagos
                .Where(p => !nuevosPagos.Any(np => np.Id == p.Id))
                .ToList();

            _context.Pagos.RemoveRange(pagosAEliminar);

            // Actualizar/agregar pagos
            foreach (var pago in nuevosPagos)
            {
                if (pago.Id > 0)
                {
                    var pagoExistente = ventaExistente.Pagos.FirstOrDefault(p => p.Id == pago.Id);
                    if (pagoExistente != null)
                    {
                        pagoExistente.MetodoPagoId = pago.MetodoPagoId;
                        pagoExistente.Monto = pago.Monto;
                        _context.Update(pagoExistente);
                    }
                }
                else
                {
                    pago.VentaId = ventaExistente.Id;
                    await _context.Pagos.AddAsync(pago);
                }
            }
        }

        private async Task CargarProductosDisponibles(string usuarioId)
        {
            var productosDisponibles = await _context.Inventarios
                .Include(i => i.Tallas)
                .Where(i => i.UsuarioId == usuarioId && i.Tallas.Any(t => t.Cantidad > 0))
                .Select(i => new
                {
                    i.Id,
                    i.Nombre,
                    i.PrecioVenta,
                    Tallas = i.Tallas.Where(t => t.Cantidad > 0).ToList()
                })
                .ToListAsync();

            ViewBag.ProductosDisponibles = productosDisponibles;
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
                .Include(v => v.NombreCliente)
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.TallaInventario)
                        .ThenInclude(t => t.Inventario)
                .Include(v => v.Pagos)
                    .ThenInclude(p => p.MetodoPago)
                .FirstOrDefaultAsync(m => m.Id == id && m.UsuarioVendedorId == usuarioActual.Id);

            if (venta == null)
            {
                return NotFound();
            }

            return View(venta);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string claveSeguridad)
        {
            if (claveSeguridad != DeletePassword)
            {
                TempData["ErrorMessage"] = "Clave de seguridad incorrecta";
                return RedirectToAction(nameof(Index));
            }

            var usuarioActual = await _userManager.GetUserAsync(User);
            var venta = await _context.Ventas
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.TallaInventario)
                .Include(v => v.Pagos)
                .FirstOrDefaultAsync(v => v.Id == id && v.UsuarioVendedorId == usuarioActual.Id);

            if (venta == null)
            {
                return NotFound();
            }

            // Crear la estrategia de ejecución para MySQL
            var executionStrategy = _context.Database.CreateExecutionStrategy();

            return await executionStrategy.ExecuteAsync(async () =>
            {
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        // Devolver los productos al inventario
                        foreach (var detalle in venta.Detalles)
                        {
                            if (detalle.TallaInventario != null)
                            {
                                detalle.TallaInventario.Cantidad += detalle.Cantidad;
                                _context.Update(detalle.TallaInventario);
                            }
                        }

                        // Eliminar los pagos asociados primero (por las restricciones de clave foránea)
                        if (venta.Pagos != null && venta.Pagos.Any())
                        {
                            _context.Pagos.RemoveRange(venta.Pagos);
                        }

                        // Eliminar los detalles de venta
                        if (venta.Detalles != null && venta.Detalles.Any())
                        {
                            _context.DetallesVenta.RemoveRange(venta.Detalles);
                        }

                        // Finalmente eliminar la venta
                        _context.Ventas.Remove(venta);

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        TempData["SuccessMessage"] = "Venta eliminada exitosamente y productos devueltos al inventario";
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Error al eliminar venta");
                        TempData["ErrorMessage"] = $"Error al eliminar la venta: {ex.Message}";
                        throw;
                    }
                }

                return RedirectToAction(nameof(Index));
            });
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
                        .ThenInclude(t => t.Inventario)
                .FirstOrDefaultAsync(v => v.Id == id && v.UsuarioVendedorId == usuarioActual.Id && v.Estado == "Completada");

            if (venta == null)
            {
                return NotFound();
            }

            return View(venta);
        }

        // POST: Ventas/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
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

            // Crear la estrategia de ejecución para MySQL
            var executionStrategy = _context.Database.CreateExecutionStrategy();

            await executionStrategy.ExecuteAsync(async () =>
            {
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        foreach (var detalle in venta.Detalles)
                        {
                            if (detalle.TallaInventario != null)
                            {
                                detalle.TallaInventario.Cantidad += detalle.Cantidad;
                                _context.Update(detalle.TallaInventario);
                            }
                        }

                        venta.Estado = "Cancelada";
                        _context.Update(venta);

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        TempData["SuccessMessage"] = "Venta cancelada exitosamente";
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Error al cancelar venta");
                        TempData["ErrorMessage"] = $"Error al cancelar la venta: {ex.Message}";
                        throw; // Relanzar la excepción para que ExecuteAsync la maneje
                    }
                }
            });

            return RedirectToAction(nameof(Index));
        }
        private bool VentaExists(int id)
        {
            return _context.Ventas.Any(e => e.Id == id);
        }
    }
}