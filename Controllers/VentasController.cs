using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tenis3t.Data;
using Tenis3t.Models;
using System.Text.Json;
using Tenis3t.Extensions;
using Microsoft.AspNetCore.Mvc.Rendering;

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

        // GET: Ventas con búsqueda
        public async Task<IActionResult> Index(string nombreCliente = null)
        {
            var usuarioActual = await _userManager.GetUserAsync(User);
            if (usuarioActual == null)
            {
                return Unauthorized();
            }

            var query = _context.Ventas
                .Include(v => v.UsuarioVendedor)
                .Include(v => v.Cliente)
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.TallaInventario)
                        .ThenInclude(t => t.Inventario)
                .Include(v => v.Pagos)
                    .ThenInclude(p => p.MetodoPago)
                .Where(v => v.UsuarioVendedorId == usuarioActual.Id)
                .OrderByDescending(v => v.FechaVenta);

            // if (!string.IsNullOrEmpty(nombreCliente))
            // {
            //     query = query.Where(v => v.Cliente != null && 
            //            !string.IsNullOrEmpty(v.Cliente.Nombre) && 
            //            v.Cliente.Nombre.ToLower().Contains(nombreCliente.ToLower()));
            // }

            var ventas = await query.ToListAsync();
            return View(ventas);
        }
        // GET: Ventas/Create (Paso 1: Selección de productos)
        public async Task<IActionResult> Create()
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

            // Recargar el inventario disponible para la vista si hay errores
            var inventarioDisponible = await _context.Inventarios
                .Include(i => i.Tallas)
                .Where(i => i.UsuarioId == usuarioActual.Id && i.Tallas.Any(t => t.Cantidad > 0))
                .ToListAsync();

            ViewBag.InventarioDisponible = inventarioDisponible;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Validar que al menos hay un producto seleccionado
            if (model.Detalles == null || !model.Detalles.Any(d => d.TallaInventarioId > 0))
            {
                ModelState.AddModelError("", "Debe seleccionar al menos un producto");
                return View(model);
            }

            // Validar disponibilidad de inventario
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

            // Guardar temporalmente en sesión para el paso 2
            HttpContext.Session.SetObject("VentaViewModel", model);

            // Redirigir al paso de métodos de pago
            return RedirectToAction("MetodosPago");
        }

        // GET: Ventas/MetodosPago (Paso 2: Selección de métodos de pago)
        public async Task<IActionResult> MetodosPago()
        {
            var model = HttpContext.Session.GetObject<VentaViewModel>("VentaViewModel");
            if (model == null || model.Detalles == null || !model.Detalles.Any())
            {
                TempData["ErrorMessage"] = "No se encontraron productos seleccionados";
                return RedirectToAction("Create");
            }

            // Calcular total de la venta
            decimal totalVenta = 0;
            foreach (var detalle in model.Detalles)
            {
                var talla = await _context.TallasInventario
                    .Include(t => t.Inventario)
                    .FirstOrDefaultAsync(t => t.Id == detalle.TallaInventarioId);

                if (talla != null && talla.Inventario != null)
                {
                    totalVenta += talla.Inventario.PrecioVenta * detalle.Cantidad;
                }
            }

            ViewBag.TotalVenta = totalVenta;

            // Obtener métodos de pago disponibles para el dropdown
            var metodosPagoDisponibles = await _context.MetodoPagos.ToListAsync();
            ViewBag.MetodosPagoDisponibles = new SelectList(metodosPagoDisponibles, "Id", "Nombre");

            // Inicializar lista vacía si es null
            model.MetodosPago ??= new List<MetodoPagoViewModel>();

            return View(model);
        }

        // POST: Ventas/MetodosPago (Paso 2: Confirmar venta con métodos de pago)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MetodosPago(VentaViewModel model)
        {
            var usuarioActual = await _userManager.GetUserAsync(User);
            if (usuarioActual == null)
            {
                return Unauthorized();
            }

            // Obtener el ViewModel completo de la sesión
            var ventaViewModel = HttpContext.Session.GetObject<VentaViewModel>("VentaViewModel");
            if (ventaViewModel == null || ventaViewModel.Detalles == null || !ventaViewModel.Detalles.Any())
            {
                TempData["ErrorMessage"] = "No se encontraron productos seleccionados";
                return RedirectToAction("Create");
            }

            // Calcular total de la venta
            decimal totalVenta = 0;
            foreach (var detalle in ventaViewModel.Detalles)
            {
                var talla = await _context.TallasInventario
                    .Include(t => t.Inventario)
                    .FirstOrDefaultAsync(t => t.Id == detalle.TallaInventarioId);

                if (talla != null && talla.Inventario != null)
                {
                    totalVenta += talla.Inventario.PrecioVenta * detalle.Cantidad;
                }
            }
            // Validar que hay al menos un método de pago
            if (model.MetodosPago == null || !model.MetodosPago.Any(m => m.Monto > 0))
            {
                ModelState.AddModelError("", "Debe agregar al menos un método de pago");
                ViewBag.TotalVenta = totalVenta;
                ViewBag.MetodosPagoDisponibles = new SelectList(await _context.MetodoPagos.ToListAsync(), "Id", "Nombre");
                return View(model);
            }

            // Filtrar solo métodos con monto > 0
            var metodosPagoValidos = model.MetodosPago.Where(m => m.Monto > 0).ToList();

            // Validar que la suma de pagos sea igual al total
            // decimal totalPagado = metodosPagoValidos.Sum(mp => mp.Monto);
            // if (totalPagado != totalVenta)
            // {
            //     ModelState.AddModelError("", $"La suma de los pagos ({totalPagado.ToString("N0")}) no coincide con el total de la venta ({totalVenta.ToString("N0")})");

            //     ViewBag.TotalVenta = totalVenta;
            //     ViewBag.MetodosPagoDisponibles = new SelectList(await _context.MetodoPagos.ToListAsync(), "Id", "Nombre");
            //     return View(model);
            // }

            // Validar referencias para métodos que las requieren
            var metodosConReferencia = await _context.MetodoPagos
                .Where(m => m.Nombre.ToLower().Contains("transferencia") ||
                            m.Nombre.ToLower().Contains("tarjeta") ||
                            m.Nombre.ToLower().Contains("credito"))
                .ToListAsync();

            foreach (var metodo in metodosPagoValidos)
            {
                var metodoPago = await _context.MetodoPagos.FindAsync(metodo.MetodoPagoId);
                if (metodoPago != null &&
                    (metodoPago.Nombre.ToLower().Contains("transferencia") ||
                     metodoPago.Nombre.ToLower().Contains("tarjeta") ||
                     metodoPago.Nombre.ToLower().Contains("credito")) &&
                    string.IsNullOrEmpty(metodo.Referencia))
                {
                    ModelState.AddModelError("", $"El método {metodoPago.Nombre} requiere un número de referencia");
                    ViewBag.TotalVenta = totalVenta;
                    ViewBag.MetodosPagoDisponibles = new SelectList(await _context.MetodoPagos.ToListAsync(), "Id", "Nombre");
                    return View(model);
                }
            }


            // Validar que la suma de pagos sea igual al total
            decimal totalPagado = model.MetodosPago?.Sum(mp => mp.Monto) ?? 0;
            if (totalPagado != totalVenta)
            {
                ModelState.AddModelError("", $"La suma de los pagos ({totalPagado.ToString("N0")}) no coincide con el total de la venta ({totalVenta.ToString("N0")})");

                ViewBag.TotalVenta = totalVenta;
                ViewBag.MetodosPago = await _context.MetodoPagos.ToListAsync();
                return View(model);
            }

            // Procesar la venta completa
            var executionStrategy = _context.Database.CreateExecutionStrategy();
            await executionStrategy.ExecuteAsync(async () =>
            {
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        // Crear cliente si se proporcionó información
                        Cliente? cliente = null;
                        if (!string.IsNullOrEmpty(ventaViewModel.NombreCliente))
                        {
                            cliente = new Cliente
                            {
                                Nombre = ventaViewModel.NombreCliente,
                                Cedula = ventaViewModel.CedulaCliente,
                                Telefono = ventaViewModel.TelefonoCliente,
                                Email = ventaViewModel.EmailCliente
                            };
                            _context.Add(cliente);
                            await _context.SaveChangesAsync();
                        }

                        // Crear venta
                        var venta = new Venta
                        {
                            FechaVenta = DateTime.Now,
                            Estado = "Completada",
                            UsuarioVendedorId = usuarioActual.Id,
                            ClienteId = cliente?.Id,
                            Total = totalVenta
                        };
                        _context.Add(venta);
                        await _context.SaveChangesAsync();

                        // Procesar detalles de venta
                        foreach (var detalle in ventaViewModel.Detalles)
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
                                PrecioUnitario = tallaInventario.Inventario.PrecioVenta
                            };

                            tallaInventario.Cantidad -= detalle.Cantidad;
                            _context.Add(detalleVenta);
                            _context.Update(tallaInventario);
                        }

                        // Procesar métodos de pago
                        if (model.MetodosPago != null)
                        {
                            foreach (var metodoPago in model.MetodosPago.Where(mp => mp.Monto > 0))
                            {
                                var pago = new Pago
                                {
                                    VentaId = venta.Id,
                                    MetodoPagoId = metodoPago.MetodoPagoId,
                                    Monto = metodoPago.Monto,
                                    Referencia = metodoPago.Referencia
                                };
                                _context.Add(pago);
                            }
                        }

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        // Limpiar sesión
                        HttpContext.Session.Remove("VentaViewModel");

                        TempData["SuccessMessage"] = "Venta registrada exitosamente";
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Error al procesar venta");
                        TempData["ErrorMessage"] = $"Error al procesar la venta: {ex.Message}";
                        throw;
                    }
                }
            });

            return RedirectToAction(nameof(Index));
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

                // Actualizar solo los campos editables
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

        // POST: Ventas/Delete/5
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
                .FirstOrDefaultAsync(v => v.Id == id && v.UsuarioVendedorId == usuarioActual.Id && v.Estado == "Completada");

            if (venta == null)
            {
                return NotFound();
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // Revertir el inventario para cada detalle
                    foreach (var detalle in venta.Detalles)
                    {
                        if (detalle.TallaInventario != null)
                        {
                            detalle.TallaInventario.Cantidad += detalle.Cantidad;
                            _context.Update(detalle.TallaInventario);
                        }
                    }

                    // Marcar la venta como cancelada
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
                }
            }

            return RedirectToAction(nameof(Index));
        }

        private bool VentaExists(int id)
        {
            return _context.Ventas.Any(e => e.Id == id);
        }
    }
}