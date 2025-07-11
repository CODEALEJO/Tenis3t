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
    }
}