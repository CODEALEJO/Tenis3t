using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using Tenis3t.Data;
using Tenis3t.Models;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;

namespace Tenis3t.Controllers
{
    [Authorize]
    public class BusquedaController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BusquedaController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Busqueda/Unificada
        public IActionResult Unificada(string nombre)
        {
            // Solo permitir al usuario 3T
            if (User.Identity?.Name != "3T")
            {
                TempData["Error"] = "Solo el usuario 3T puede acceder a esta función";
                return RedirectToAction("Index", "Home");
            }

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

            // Buscar en inventario
            var enInventario = _context.Inventarios
                .Include(i => i.Tallas)
                .Include(i => i.Usuario)
                .Where(i => i.Nombre.ToLower().Contains(nombre.ToLower()))
                .ToList();

            foreach (var inventario in enInventario)
            {
                resultados.EnInventario.Add(new ResultadoInventario
                {
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

            // Buscar en exhibiciones
            var enExhibicion = _context.Prestamos
                .Include(p => p.TallaInventario)
                    .ThenInclude(ti => ti.Inventario)
                .Include(p => p.UsuarioReceptor)
                .Where(p => p.TallaInventario.Inventario.Nombre.ToLower().Contains(nombre.ToLower()) && 
                           p.Estado == "Prestado")
                .ToList();

            foreach (var exhibicion in enExhibicion)
            {
                resultados.EnExhibicion.Add(new ResultadoExhibicion
                {
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