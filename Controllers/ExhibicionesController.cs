using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using Tenis3t.Data;
using Tenis3t.Models;

namespace Tenis3t.Controllers
{
    public class ExhibicionesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ExhibicionesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string cliente)
        {
            if (User.Identity?.Name != "3T")
            {
                return Forbid(); // O puedes redirigir a una página de error
            }

            // Consulta base que incluye las relaciones necesarias
            var query = _context.Prestamos
                .Include(p => p.TallaInventario)
                    .ThenInclude(ti => ti.Inventario)
                .Include(p => p.UsuarioPrestamista)
                .Include(p => p.UsuarioReceptor)
                .Where(p => p.UsuarioPrestamista.UserName == "3T") // Solo exhibiciones hechas por 3T
                .OrderByDescending(p => p.FechaPrestamo)
                .AsQueryable();

            // Aplicar filtro si se especificó un cliente
            if (!string.IsNullOrEmpty(cliente))
            {
                query = query.Where(p => p.UsuarioReceptor.UserName.Contains(cliente));
            }

            // Ejecutar la consulta y pasar los resultados a la vista
            var exhibiciones = await query.ToListAsync();
            ViewBag.ClienteFiltrado = cliente; // Para mantener el valor del filtro en la vista

            return View(exhibiciones);
        }

        public async Task<IActionResult> ConteoGeneral()
        {
            if (User.Identity?.Name != "3T")
            {
                return Forbid();
            }

            // Obtener exhibiciones activas (prestadas) - Ya está filtrado por 3T
            var exhibicionesActivas = await _context.Prestamos
                .Include(p => p.TallaInventario)
                    .ThenInclude(ti => ti.Inventario)
                .Include(p => p.UsuarioReceptor)
                .Where(p => p.UsuarioPrestamista.UserName == "3T" && p.Estado == "Prestado")
                .OrderBy(p => p.UsuarioReceptor.UserName)
                .ToListAsync();

            // Obtener inventario disponible SOLO del usuario 3T
            var inventarioDisponible = await _context.Inventarios
                .Include(i => i.Tallas)
                .Where(i => i.Usuario.UserName == "3T" && i.Tallas.Any(t => t.Cantidad > 0)) // Filtro por usuario 3T
                .OrderBy(i => i.Nombre)
                .ToListAsync();

            // Calcular totales
            var totalExhibiciones = exhibicionesActivas.Sum(e => e.Cantidad);
            var totalInventario = inventarioDisponible
                .Sum(i => i.Tallas.Sum(t => t.Cantidad));

            var viewModel = new ConteoGeneralViewModel
            {
                Exhibiciones = exhibicionesActivas,
                Inventario = inventarioDisponible,
                TotalExhibiciones = totalExhibiciones,
                TotalInventario = totalInventario
            };

            return View(viewModel);
        }
    }
}