using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace WebAPIAutores.Controllers
{
    public class CustomBaseController:ControllerBase
    {
        protected string ObtenreUsuarioId() 
        {
            var usuarioClaim = HttpContext.User.Claims.Where(w => w.Type == "id").FirstOrDefault();
            var usuarioId = usuarioClaim.Value;
            return usuarioId;
        }
    }
}
