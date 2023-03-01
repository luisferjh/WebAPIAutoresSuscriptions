
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WebAPIAutores.DTOs;
using WebAPIAutores.Entidades;

namespace WebAPIAutores.Middlewares
{
    public static class LimitarPeticionesMiddlewareExtensions
    {
        public static IApplicationBuilder UseLimitarPeticiones(this IApplicationBuilder app)
        {
            return app.UseMiddleware<LimitarPeticionesMiddleware>();
        }
    }

    public class LimitarPeticionesMiddleware
    {
        private readonly RequestDelegate siguiente;
        private readonly IConfiguration configuration;

        public LimitarPeticionesMiddleware(RequestDelegate siguiente, IConfiguration configuration) 
        {
            this.siguiente = siguiente;
            this.configuration = configuration;
        }

        public async Task InvokeAsync(HttpContext httpContext, ApplicationDbContext context) 
        {
            var limitarPeticionesConfiguracion = new LimitarPeticionesConfiguracion();
            configuration.GetRequiredSection("limitarPeticiones").Bind(limitarPeticionesConfiguracion);

            var ruta = httpContext.Request.Path.ToString();
            var estaLaRutaEnListaBlanca = limitarPeticionesConfiguracion.ListaBlancaRutas.Any(x => ruta.Contains(x));

            if (estaLaRutaEnListaBlanca)
            {
                await siguiente(httpContext);
                return;
            }

            var llavesStringValues = httpContext.Request.Headers["x-Api-Key"];

            if (llavesStringValues.Count == 0)
            {
                httpContext.Response.StatusCode = 400;
                await httpContext.Response.WriteAsync("Debe proveer la llave en la cabecera x-Api-Key");
                return;
            }

            if (llavesStringValues.Count > 1)
            {
                httpContext.Response.StatusCode = 400;
                await httpContext.Response.WriteAsync("Solo una llave debe estar presente");
                return;
            }

            var llave = llavesStringValues[0];

            var llaveDB = await context.LlavesAPI
                .Include(x => x.RestriccionesDominio)
                .Include(x => x.RestriccionesIP)
                .Include(x => x.Usuario)
                .FirstOrDefaultAsync(f => f.Llave == llave);

            if (llaveDB == null)
            {
                httpContext.Response.StatusCode = 400;
                await httpContext.Response.WriteAsync("la llave no existe");
                return;
            }

            if (!llaveDB.Activa)
            {
                httpContext.Response.StatusCode = 400;
                await httpContext.Response.WriteAsync("la llave se encuentra inactiva");
                return;
            }

            if (llaveDB.TipoLlave == TipoLlave.Gratuita)
            {
                var hoy = DateTime.Today;
                var mañana = hoy.AddDays(1);
                var cantidadPeticionesRealiadasHoy = await context.Peticiones.CountAsync(x =>
                    x.LlaveId == llaveDB.Id &&
                    x.FechaPeticion >= hoy &&
                    x.FechaPeticion < mañana);

                if (cantidadPeticionesRealiadasHoy >= limitarPeticionesConfiguracion.PeticionesPorDiaGratuito)
                {
                    httpContext.Response.StatusCode = 429; // Too many request
                    await httpContext.Response.WriteAsync("Ha excedido el limite de peticiones por día. " +
                        "Si desea realizar mas peticiones, actualice su suscripción a una cuenta profesional");
                    return;
                }
            }
            else if(llaveDB.Usuario.MalaPaga)
            {
                httpContext.Response.StatusCode = 400;
                await httpContext.Response.WriteAsync("El usuario es un mala Paga");
                return;
            }

            var superaRestricciones = PeticionSuperaAlgunaDeLasRestricciones(llaveDB, httpContext);

            if (!superaRestricciones)
            {
                httpContext.Response.StatusCode = 403;
                return;
            }

            var peticion = new Peticion() { LlaveId = llaveDB.Id, FechaPeticion = DateTime.UtcNow };
            context.Add(peticion);
            await context.SaveChangesAsync();

            await siguiente(httpContext);
        }
      

        private bool PeticionSuperaAlgunaDeLasRestricciones(LlaveAPI llaveAPI, HttpContext httpContext) 
        {
            var hayRestricciones = llaveAPI.RestriccionesDominio.Any() || llaveAPI.RestriccionesIP.Any();

            if (!hayRestricciones)
            {
                return true;
            }

            var peticionSuperaLasRestriccionesDeDominio = PeticionSuperaLasRestriccionesDominio(llaveAPI.RestriccionesDominio, httpContext);
            
            var peticionSuperaLasRestriccionesDeIP = PeticionSuperaLasRestriccionesPorIP(llaveAPI.RestriccionesIP, httpContext);

            return peticionSuperaLasRestriccionesDeDominio || peticionSuperaLasRestriccionesDeIP;
        }

        private bool PeticionSuperaLasRestriccionesPorIP(List<RestriccionIP> restricciones, HttpContext httpContext)
        {
            if(restricciones == null || restricciones.Count == 0)
            {
                return false;
            }

            var IP = httpContext.Connection.RemoteIpAddress.ToString();

            if (IP == string.Empty)
            {
                return false;
            }

            var superaRestriccion = restricciones.Any(x => x.IP == IP);
            return superaRestriccion;
        }

        private bool PeticionSuperaLasRestriccionesDominio(List<RestriccionDominio> restricciones,
            HttpContext httpContext) 
        {
            if (restricciones == null || restricciones.Count == 0)
            {
                return false;
            }

            var referer = httpContext.Request.Headers["Referer"].ToString();

            if (referer == string.Empty)
            {
                return false;
            }

            Uri myUri = new Uri(referer);
            string host = myUri.Host;

            var superaRestriccion = restricciones.Any(x => x.Dominio == host);
            return superaRestriccion;
        }
    }
}
