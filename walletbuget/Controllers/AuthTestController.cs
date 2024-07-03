using walletbuget.Models;
using walletbuget.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace walletbuget.Controllers
{
    [Route("api/AuthTest")]
    [ApiController]
    public class AuthTestController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public AuthTestController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<ActionResult<string>> VerificarRol()
        {
            string usuarioId = await ObtenerUsuarioId();

            if (usuarioId == null)
            {
                return Unauthorized(); // Devuelve un código de estado 401 (No autorizado)
            }

            if (await EsUsuarioFree(usuarioId))
            {
                return "Tu rol actual es Free";
            }

            return "Tu rol actual es Premium";
        }

        private async Task<string> ObtenerUsuarioId()
        {
            string idActual = HttpContext.User.FindFirstValue("id");
            return idActual;
        }

        private async Task<bool> EsUsuarioFree(string usuarioId)
        {
            var usuario = await _userManager.FindByIdAsync(usuarioId);
            if (usuario == null) return false;

            var roles = await _userManager.GetRolesAsync(usuario);
            return roles.Contains("free");
        }
    }
}
