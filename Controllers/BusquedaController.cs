using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using Tenis3t.Data;
using Tenis3t.Models;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Tenis3t.Controllers
{
        public class BusquedaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public BusquedaController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Busqueda/Unificada
        public async Task<IActionResult> Unificada(string nombre)
        {
            // Obtener usuario actual
            var usuarioActualId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(nombre))
            {
                return View(new BusquedaUnificadaViewModel
                {
                    NombreProducto = "",
                    EnInventario = new List<ResultadoInventario>(),
                    EnExhibicion = new List<ResultadoExhibicion>()
                });
            }

            var resultados = new BusquedaUnificadaViewModel
            {
                NombreProducto = nombre,
                EnInventario = new List<ResultadoInventario>(),
                EnExhibicion = new List<ResultadoExhibicion>()
            };

            // Buscar en inventario SOLO del usuario actual
            var enInventario = await _context.Inventarios
                .Include(i => i.Tallas)
                .Include(i => i.Usuario)
                .Where(i => i.UsuarioId == usuarioActualId &&
                            i.Nombre.ToLower().Contains(nombre.ToLower()))
                .ToListAsync();

            foreach (var inventario in enInventario)
            {
                resultados.EnInventario.Add(new ResultadoInventario
                {
                    NombreProducto = inventario.Nombre, // ✅ Agregado
                    Usuario = inventario.Usuario.UserName,
                    CantidadTotal = inventario.Cantidad,
                    PrecioVenta = inventario.PrecioVenta,
                    Tallas = inventario.Tallas.Select(t => new TallaCantidad
                    {
                        Talla = t.Talla,
                        Cantidad = t.Cantidad
                    }).ToList()
                });
            }

            // Buscar en exhibiciones SOLO del usuario actual
            var enExhibicion = await _context.Prestamos
                .Include(p => p.TallaInventario)
                    .ThenInclude(ti => ti.Inventario)
                .Include(p => p.UsuarioReceptor)
                .Where(p => p.TallaInventario.Inventario.UsuarioId == usuarioActualId &&
                            p.TallaInventario.Inventario.Nombre.ToLower().Contains(nombre.ToLower()) &&
                            p.Estado == "Prestado")
                .ToListAsync();

            foreach (var exhibicion in enExhibicion)
            {
                resultados.EnExhibicion.Add(new ResultadoExhibicion
                {
                    NombreProducto = exhibicion.TallaInventario.Inventario.Nombre, // ✅ Agregado
                    Cliente = exhibicion.UsuarioReceptor.UserName,
                    Cantidad = exhibicion.Cantidad,
                    Talla = exhibicion.TallaInventario.Talla,
                    FechaPrestamo = exhibicion.FechaPrestamo
                });
            }

            return View(resultados);
        }
    }
}
