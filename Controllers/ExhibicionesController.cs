using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using Tenis3t.Data;
using Tenis3t.Helpers;
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
            if (!PermissionConstants.IsAdminUser(User.Identity?.Name))
            {
                return Forbid();
            }

            // ✅ FILTRAR POR EL USUARIO ACTUAL, NO POR TODOS LOS ADMINS
            var currentUser = User.Identity.Name;
            var query = _context.Prestamos
                .Include(p => p.TallaInventario)
                    .ThenInclude(ti => ti.Inventario)
                .Include(p => p.UsuarioPrestamista)
                .Include(p => p.UsuarioReceptor)
                .Where(p => p.UsuarioPrestamista.UserName == currentUser)  // ✅ SOLO DEL USUARIO ACTUAL
                .OrderByDescending(p => p.FechaPrestamo)
                .AsQueryable();

            if (!string.IsNullOrEmpty(cliente))
            {
                query = query.Where(p => p.UsuarioReceptor.UserName.Contains(cliente));
            }

            var exhibiciones = await query.ToListAsync();
            ViewBag.ClienteFiltrado = cliente;
            return View(exhibiciones);
        }

        public async Task<IActionResult> ConteoGeneral()
        {
            if (!PermissionConstants.IsAdminUser(User.Identity?.Name))
            {
                return Forbid();
            }

            // ✅ FILTRAR POR EL USUARIO ACTUAL
            var currentUser = User.Identity.Name;

            // Obtener exhibiciones activas del USUARIO ACTUAL
            var exhibicionesActivas = await _context.Prestamos
                .Include(p => p.TallaInventario)
                    .ThenInclude(ti => ti.Inventario)
                .Include(p => p.UsuarioReceptor)
                .Where(p => p.UsuarioPrestamista.UserName == currentUser &&  // ✅ SOLO DEL USUARIO ACTUAL
                           p.Estado == "Prestado")
                .OrderBy(p => p.UsuarioReceptor.UserName)
                .ToListAsync();

            // Obtener inventario disponible del USUARIO ACTUAL
            var inventarioDisponible = await _context.Inventarios
                .Include(i => i.Tallas)
                .Where(i => i.Usuario.UserName == currentUser &&  // ✅ SOLO DEL USUARIO ACTUAL
                           i.Tallas.Any(t => t.Cantidad > 0))
                .OrderBy(i => i.Nombre)
                .ToListAsync();

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

        public async Task<IActionResult> ImprimirExhibiciones(string usuarioReceptor)
        {
            if (!PermissionConstants.IsAdminUser(User.Identity?.Name))
            {
                return Forbid();
            }

            if (string.IsNullOrEmpty(usuarioReceptor))
            {
                return RedirectToAction(nameof(Index));
            }

            // ✅ FILTRAR POR EL USUARIO ACTUAL
            var currentUser = User.Identity.Name;

            // Consulta para obtener las exhibiciones del USUARIO ACTUAL
            var exhibiciones = await _context.Prestamos
                .Include(p => p.TallaInventario)
                    .ThenInclude(ti => ti.Inventario)
                .Include(p => p.UsuarioReceptor)
                .Where(p => p.UsuarioPrestamista.UserName == currentUser &&  // ✅ SOLO DEL USUARIO ACTUAL
                           p.UsuarioReceptor.UserName == usuarioReceptor)
                .OrderByDescending(p => p.FechaPrestamo)
                .ToListAsync();

            ViewBag.UsuarioReceptor = usuarioReceptor;
            ViewBag.FechaImpresion = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

            return View(exhibiciones);
        }
    }
}