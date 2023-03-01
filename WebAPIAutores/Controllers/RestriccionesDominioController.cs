using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Any;
using System.Threading.Tasks;
using WebAPIAutores.DTOs;
using WebAPIAutores.Entidades;
using WebAPIAutores.Migrations;

namespace WebAPIAutores.Controllers
{
    [ApiController]
    [Route("api/restricciondominio")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class RestriccionesDominioController:CustomBaseController
    {
        private readonly ApplicationDbContext context;

        public RestriccionesDominioController(ApplicationDbContext context)
        {
            this.context = context;
        }

        [HttpPost]
        public async Task<ActionResult> Post(CrearRestriccionesDominioDTO crearRestriccionesDominioDTO) 
        {
            var llaveDB = await context.LlavesAPI.FirstOrDefaultAsync(x => x.Id == crearRestriccionesDominioDTO.LlaveId);

            if (llaveDB == null)
            {
                return NotFound();
            }

            var usuarioId = ObtenreUsuarioId();

            if (llaveDB.UsuarioId != usuarioId)
            {
                return Forbid();
            }

            var restriccionDominio = new RestriccionDominio()
            {
                LlaveId = crearRestriccionesDominioDTO.LlaveId,
                Dominio = crearRestriccionesDominioDTO.Dominio
            };

            context.Add(restriccionDominio);
            await context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPut("{id}")]
        public async Task<ActionResult> Put(int id, ActualizarRestriccionDominioDTO actualizarRestriccionDominioDTO)
        {
            var restriccionDB = await context.RestriccionesDominio
                .Include(x => x.Llave)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (restriccionDB == null)
            {
                return Forbid();
            }

            var usuarioId = ObtenreUsuarioId();


            if (restriccionDB.Llave.UsuarioId != usuarioId)
            {
                return Forbid();
            }

            restriccionDB.Dominio = actualizarRestriccionDominioDTO.Dominio;

            await context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)         
        {
            var restriccionDB = await context.RestriccionesDominio
                .Include(x => x.Id ==  id)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (restriccionDB == null)
            {
                return NotFound();
            }

            var usuarioId = ObtenreUsuarioId();

            if (usuarioId != restriccionDB.Llave.UsuarioId)
            {
                return Forbid();
            }

            context.Remove(restriccionDB);
            await context.SaveChangesAsync();
            return NoContent();
        }
    }
}
