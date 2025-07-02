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

        // GET: Ventas con búsqueda y filtros avanzados
        // Reemplaza el método Index en tu VentasController con esta versión corregida

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

            // Consulta base con includes
            var query = _context.Ventas
                .Include(v => v.UsuarioVendedor)
                .Include(v => v.Cliente)
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.TallaInventario)
                        .ThenInclude(t => t.Inventario)
                .Include(v => v.Pagos)
                    .ThenInclude(p => p.MetodoPago)
                .Where(v => v.UsuarioVendedorId == usuarioActual.Id)
                .AsQueryable();

            // Filtro por nombre de cliente
            if (!string.IsNullOrEmpty(nombreCliente))
            {
                query = query.Where(v => v.Cliente != null && v.Cliente.Nombre.Contains(nombreCliente));
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
            if (usuarioActual == null)
            {
                return Unauthorized();
            }

            var productosDisponibles = await _context.Inventarios
                .Where(i => i.UsuarioId == usuarioActual.Id && i.Tallas.Any(t => t.Cantidad > 0))
                .Select(i => new ProductoDisponibleViewModel
                {
                    Id = i.Id,
                    Nombre = i.Nombre,
                    PrecioVenta = i.PrecioVenta,
                    Tallas = i.Tallas.Where(t => t.Cantidad > 0).ToList()
                })
                .ToListAsync();

            ViewBag.ProductosDisponibles = productosDisponibles;
            ViewBag.InventarioDisponible = productosDisponibles; // Para el JavaScript

            return View(new VentaViewModel());
        }
        // POST: Ventas/Create (Paso 1: Guardar productos y pasar a métodos de pago)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(VentaViewModel model)
        {
            var usuarioActual = await _userManager.GetUserAsync(User);
            if (usuarioActual == null)
            {
                return Unauthorized();
            }

            var inventarioDisponible = await _context.Inventarios
                .Include(i => i.Tallas)
                .Where(i => i.UsuarioId == usuarioActual.Id && i.Tallas.Any(t => t.Cantidad > 0))
                .ToListAsync();

            ViewBag.InventarioDisponible = inventarioDisponible;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.Detalles == null || !model.Detalles.Any(d => d.TallaInventarioId > 0))
            {
                ModelState.AddModelError("", "Debe seleccionar al menos un producto");
                return View(model);
            }

            var erroresStock = new List<string>();
            foreach (var detalle in model.Detalles.Where(d => d.TallaInventarioId > 0))
            {
                var talla = await _context.TallasInventario
                    .FirstOrDefaultAsync(t => t.Id == detalle.TallaInventarioId);

                if (talla == null)
                {
                    erroresStock.Add($"El producto seleccionado (Talla ID: {detalle.TallaInventarioId}) no existe");
                }
                else if (talla.Cantidad < detalle.Cantidad)
                {
                    erroresStock.Add($"No hay suficiente stock para {talla.Inventario?.Nombre} - Talla: {talla.Talla} (Solicitado: {detalle.Cantidad}, Disponible: {talla.Cantidad})");
                }
            }

            if (erroresStock.Any())
            {
                foreach (var error in erroresStock)
                {
                    ModelState.AddModelError("", error);
                }
                return View(model);
            }

            HttpContext.Session.SetObject("VentaViewModel", model);
            return RedirectToAction("MetodosPago");
        }

        // GET: Ventas/MetodosPago
        public async Task<IActionResult> MetodosPago()
        {
            var model = HttpContext.Session.GetObject<VentaViewModel>("VentaViewModel");
            if (model == null || model.Detalles == null || !model.Detalles.Any())
            {
                TempData["ErrorMessage"] = "No se encontraron productos seleccionados";
                return RedirectToAction("Create");
            }

            decimal totalVenta = model.Detalles.Sum(d => d.PrecioUnitario * d.Cantidad);
            ViewBag.TotalVenta = totalVenta;

            var metodosPagoDisponibles = await _context.MetodoPagos
                .OrderBy(m => m.Nombre)
                .Select(m => new SelectListItem
                {
                    Value = m.Id.ToString(),
                    Text = m.Nombre
                })
                .ToListAsync();

            ViewBag.MetodosPagoDisponibles = metodosPagoDisponibles;

            if (model.MetodosPago == null || !model.MetodosPago.Any())
            {
                model.MetodosPago = new List<MetodoPagoViewModel>
                {
                    new MetodoPagoViewModel()
                };
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MetodosPago(VentaViewModel model)
        {
            var usuarioActual = await _userManager.GetUserAsync(User);
            if (usuarioActual == null) return Unauthorized();

            var ventaViewModel = HttpContext.Session.GetObject<VentaViewModel>("VentaViewModel");
            if (ventaViewModel == null || ventaViewModel.Detalles == null || !ventaViewModel.Detalles.Any())
            {
                TempData["ErrorMessage"] = "No se encontraron productos seleccionados";
                return RedirectToAction("Create");
            }

            decimal totalVenta = ventaViewModel.Detalles.Sum(d => d.PrecioUnitario * d.Cantidad);
            ViewBag.TotalVenta = totalVenta;

            // Validar que no haya más de 4 métodos de pago
            if (model.MetodosPago != null && model.MetodosPago.Count > 4)
            {
                ModelState.AddModelError("", "No se pueden agregar más de 4 métodos de pago");
                return CargarViewBagYRetornarVista(model);
            }

            // Filtrar solo métodos de pago válidos (con monto > 0 y método seleccionado)
            var metodosPagoValidos = model.MetodosPago?
                .Where(m => m.Monto > 0 && m.MetodoPagoId > 0)
                .ToList() ?? new List<MetodoPagoViewModel>();

            if (!metodosPagoValidos.Any())
            {
                ModelState.AddModelError("", "Debe agregar al menos un método de pago");
                return CargarViewBagYRetornarVista(model);
            }

            decimal totalPagado = metodosPagoValidos.Sum(mp => mp.Monto);
            if (Math.Abs(totalPagado - totalVenta) > 0.01m)
            {
                ModelState.AddModelError("",
                    $"La suma de los pagos ({totalPagado.ToString("C", new CultureInfo("es-CO"))}) " +
                    $"no coincide con el total de la venta ({totalVenta.ToString("C", new CultureInfo("es-CO"))})");
                return CargarViewBagYRetornarVista(model);
            }

            return await ProcesarVenta(ventaViewModel, metodosPagoValidos, usuarioActual.Id);
        }

        private IActionResult CargarViewBagYRetornarVista(VentaViewModel model)
        {
            // Cargar métodos de pago disponibles para el dropdown
            var metodosPagoDisponibles = _context.MetodoPagos
                .OrderBy(m => m.Nombre)
                .Select(m => new SelectListItem
                {
                    Value = m.Id.ToString(),
                    Text = m.Nombre
                })
                .ToList();

            ViewBag.MetodosPagoDisponibles = metodosPagoDisponibles;

            // Si no hay métodos de pago en el modelo, agregar uno vacío
            if (model.MetodosPago == null || !model.MetodosPago.Any())
            {
                model.MetodosPago = new List<MetodoPagoViewModel>
        {
            new MetodoPagoViewModel()
        };
            }

            return View("MetodosPago", model);
        }
        private async Task<IActionResult> ProcesarVenta(VentaViewModel model, List<MetodoPagoViewModel> metodosPago, string usuarioId)
        {
            decimal totalVenta = model.Detalles.Sum(d => d.PrecioUnitario * d.Cantidad);

            var executionStrategy = _context.Database.CreateExecutionStrategy();

            return await executionStrategy.ExecuteAsync(async () =>
            {
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        Cliente? cliente = null;
                        if (!string.IsNullOrEmpty(model.NombreCliente))
                        {
                            cliente = new Cliente
                            {
                                Nombre = model.NombreCliente,
                                Cedula = model.CedulaCliente,
                                Telefono = model.TelefonoCliente,
                                Email = model.EmailCliente,
                            };
                            _context.Add(cliente);
                            await _context.SaveChangesAsync();
                        }

                        var venta = new Venta
                        {
                            FechaVenta = DateTime.Now,
                            Estado = "Completada",
                            UsuarioVendedorId = usuarioId,
                            ClienteId = cliente?.Id,
                            Total = totalVenta
                        };
                        _context.Add(venta);
                        await _context.SaveChangesAsync();

                        foreach (var detalle in model.Detalles)
                        {
                            var tallaInventario = await _context.TallasInventario
                                .Include(t => t.Inventario)
                                .FirstOrDefaultAsync(t => t.Id == detalle.TallaInventarioId);

                            if (tallaInventario == null || tallaInventario.Cantidad < detalle.Cantidad)
                            {
                                throw new Exception($"Error en el inventario para la talla ID: {detalle.TallaInventarioId}");
                            }

                            var detalleVenta = new DetalleVenta
                            {
                                VentaId = venta.Id,
                                TallaInventarioId = detalle.TallaInventarioId,
                                Cantidad = detalle.Cantidad,
                                PrecioUnitario = detalle.PrecioUnitario
                            };

                            tallaInventario.Cantidad -= detalle.Cantidad;
                            _context.Add(detalleVenta);
                            _context.Update(tallaInventario);
                        }

                        foreach (var metodoPago in metodosPago)
                        {
                            var pago = new Pago
                            {
                                VentaId = venta.Id,
                                MetodoPagoId = metodoPago.MetodoPagoId,
                                Monto = metodoPago.Monto
                            };
                            _context.Add(pago);
                        }

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        HttpContext.Session.Remove("VentaViewModel");
                        TempData["SuccessMessage"] = "Venta registrada exitosamente";
                        return RedirectToAction(nameof(Index));
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Error al procesar venta");
                        TempData["ErrorMessage"] = $"Error al procesar la venta: {ex.Message}";
                        return CargarViewBagYRetornarVista(model);
                    }
                }
            });
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
                .Include(v => v.Cliente)
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

        // GET: Ventas/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var usuarioActual = await _userManager.GetUserAsync(User);
            var venta = await _context.Ventas
                .Include(v => v.UsuarioVendedor)
                .Include(v => v.Cliente)
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.TallaInventario)
                        .ThenInclude(t => t.Inventario)
                .Include(v => v.Pagos)
                    .ThenInclude(p => p.MetodoPago)
                .FirstOrDefaultAsync(v => v.Id == id && v.UsuarioVendedorId == usuarioActual.Id);

            if (venta == null)
            {
                return NotFound();
            }

            if (venta.Estado != "Completada")
            {
                TempData["ErrorMessage"] = "Solo se pueden editar ventas completadas";
                return RedirectToAction(nameof(Index));
            }

            return View(venta);
        }

        // POST: Ventas/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Venta venta)
        {
            if (id != venta.Id)
            {
                return NotFound();
            }

            var usuarioActual = await _userManager.GetUserAsync(User);

            try
            {
                var ventaExistente = await _context.Ventas
                    .Include(v => v.Cliente)
                    .FirstOrDefaultAsync(v => v.Id == id && v.UsuarioVendedorId == usuarioActual.Id);

                if (ventaExistente == null)
                {
                    return NotFound();
                }

                if (ventaExistente.Cliente == null && venta.Cliente != null)
                {
                    ventaExistente.Cliente = new Cliente
                    {
                        Nombre = venta.Cliente.Nombre,
                        Cedula = venta.Cliente.Cedula,
                        Telefono = venta.Cliente.Telefono,
                        Email = venta.Cliente.Email
                    };
                }
                else if (ventaExistente.Cliente != null && venta.Cliente != null)
                {
                    ventaExistente.Cliente.Nombre = venta.Cliente.Nombre;
                    ventaExistente.Cliente.Cedula = venta.Cliente.Cedula;
                    ventaExistente.Cliente.Telefono = venta.Cliente.Telefono;
                    ventaExistente.Cliente.Email = venta.Cliente.Email;
                }

                _context.Update(ventaExistente);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Venta actualizada exitosamente";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!VentaExists(venta.Id))
                {
                    return NotFound();
                }
                else
                {
                    _logger.LogError(ex, "Error al actualizar venta");
                    TempData["ErrorMessage"] = "Error al actualizar la venta";
                    throw;
                }
            }
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