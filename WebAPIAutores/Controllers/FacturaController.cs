using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using WebAPIAutores.DTOs;
using WebAPIAutores.Entidades;

namespace WebAPIAutores.Controllers
{
    [ApiController]
    [Route("api/factura")]
    public class FacturaController:ControllerBase
    {
        private readonly ApplicationDbContext context;

        public FacturaController(ApplicationDbContext context)
        {
            this.context = context;
        }

        [HttpPost]
        public async Task<ActionResult> Pagar(PagarFacturaDTO pagarFacturaDTO)
        {
            var facturaDB = await context.Facturas
                .Include(x => x.Usuario)
                .FirstOrDefaultAsync(x => x.Id == pagarFacturaDTO.FacturaId);

            if (facturaDB == null) 
            {
                return NotFound();
            }

            if (facturaDB.Pagada)
            {
                return BadRequest("La factura ya fue saldada");
            }

            // logica para pagar la factura
            // nosotros vamos a pretender que el pago fue exitoso

            facturaDB.Pagada = true;
            await context.SaveChangesAsync();

            var hayFacturaPendientesVenciadas = await context.Facturas
                .AnyAsync(x => x.UsuarioId == facturaDB.UsuarioId &&
                !x.Pagada && x.FechaLimitePago < DateTime.Today);

            return NoContent();
        }
    }
}
