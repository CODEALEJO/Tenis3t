using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tenis3t.Data;
using Tenis3t.Models;
using Microsoft.AspNetCore.Identity;

namespace Tenis3t.Controllers
{
    public class SalidasController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public SalidasController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }


        [HttpGet]
        public async Task<IActionResult> Crear(int id)
        {
            var usuario = await _userManager.GetUserAsync(User);
            var inventario = await _context.Inventarios
                .Include(i => i.Tallas)
                .FirstOrDefaultAsync(i => i.Id == id && i.UsuarioId == usuario.Id);

            if (inventario == null)
                return NotFound();

            ViewBag.Tallas = inventario.Tallas
     .Where(t => t.Cantidad > 0)
     .Select(t => new TallaSelectViewModel
     {
         Id = t.Id,
         Texto = $"Talla {t.Talla} ({t.Cantidad} disponibles)"
     })
     .ToList();


            return View(inventario);
        }

        [HttpPost]
        public async Task<IActionResult> Crear(int inventarioId, int tallaId, int cantidad, string nota)
        {
            var usuario = await _userManager.GetUserAsync(User);

            var talla = await _context.TallasInventario
                .Include(t => t.Inventario)
                .FirstOrDefaultAsync(t => t.Id == tallaId && t.Inventario.UsuarioId == usuario.Id);

            if (talla == null)
            {
                TempData["ErrorMessage"] = "Talla no encontrada.";
                return RedirectToAction("Index", "Inventario");
            }

            if (cantidad <= 0)
            {
                ModelState.AddModelError("cantidad", "La cantidad debe ser mayor a 0.");
            }

            if (talla.Cantidad < cantidad)
            {
                ModelState.AddModelError("cantidad", $"Cantidad disponible de esa talla: {talla.Cantidad}. No puede superar el stock.");
            }

            if (!ModelState.IsValid)
            {
                // Volvemos a cargar las tallas para la vista
                var inventario = await _context.Inventarios
                    .Include(i => i.Tallas)
                    .FirstOrDefaultAsync(i => i.Id == inventarioId && i.UsuarioId == usuario.Id);

                ViewBag.Tallas = inventario.Tallas
                    .Where(t => t.Cantidad > 0)
                    .Select(t => new TallaSelectViewModel
                    {
                        Id = t.Id,
                        Texto = $"Talla {t.Talla} ({t.Cantidad} disponibles)"
                    })
                    .ToList();

                return View(inventario); // <- Regresa a la misma vista con los errores
            }

            // ✅ Si pasa la validación
            talla.Cantidad -= cantidad;

            var salida = new Salida
            {
                InventarioId = inventarioId,
                TallaInventarioId = tallaId,
                Cantidad = cantidad,
                Nota = nota
            };

            _context.Salidas.Add(salida);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Salida registrada correctamente.";
            return RedirectToAction("Index", "Inventario");
        }


    }
}
