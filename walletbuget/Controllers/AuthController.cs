using walletbuget.Data;
using walletbuget.Models;
using walletbuget.Models.Dto;
using walletbuget.Utility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;

namespace walletbuget.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private ApiResponse _response;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private string secretKey;

        public AuthController(ApplicationDbContext db, IConfiguration configuration,
            UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _db = db;
            secretKey = configuration.GetValue<string>("ApiSettings:Secret");
            _response = new ApiResponse();
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDTO model)
        {
            ApplicationUser userFromDb = _db.ApplicationUsers
                .FirstOrDefault(u => u.UserName.ToLower() == model.UserName.ToLower());

            bool isValid = await _userManager.CheckPasswordAsync(userFromDb, model.Password);

            if (isValid == false)
            {
                _response.Result = new LoginResponseDTO();
                _response.StatusCode = HttpStatusCode.BadRequest;
                _response.IsSuccess = false;
                _response.ErrorMessages.Add("Nombre de usuario o contraseña incorrecto");
                return BadRequest(_response);
            }

            //Tenemos que generar JWT Token
            var roles = await _userManager.GetRolesAsync(userFromDb);

            JwtSecurityTokenHandler tokenHandler = new();
            byte[] key = Encoding.ASCII.GetBytes(secretKey);

            SecurityTokenDescriptor tokenDescriptor = new()
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim("fullName",userFromDb.Name),
                    new Claim("id",userFromDb.Id.ToString()),
                    new Claim(ClaimTypes.Email, userFromDb.UserName.ToString()),
                    new Claim(ClaimTypes.Role, roles.FirstOrDefault())
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);

            LoginResponseDTO loginResponse = new()
            {
                Email = userFromDb.Email,
                Token = tokenHandler.WriteToken(token)
            };

            if (loginResponse.Email == null || string.IsNullOrEmpty(loginResponse.Token))
            {
                _response.StatusCode = HttpStatusCode.BadRequest;
                _response.IsSuccess = false;
                _response.ErrorMessages.Add("Nombre de usuario o contraseña incorrecto");
                return BadRequest(_response);
            }

            _response.StatusCode = HttpStatusCode.OK;
            _response.IsSuccess = true;
            _response.Result = loginResponse;
            return Ok(_response);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDTO model)
        {
            ApplicationUser userFromDb = _db.ApplicationUsers
                .FirstOrDefault(u => u.UserName.ToLower() == model.UserName.ToLower());

            if (userFromDb != null)
            {
                _response.StatusCode = HttpStatusCode.BadRequest;
                _response.IsSuccess = false;
                _response.ErrorMessages.Add("El usuario ya existe");
                return BadRequest(_response);
            }

            ApplicationUser newUser = new()
            {
                UserName = model.UserName,
                Email = model.UserName,
                NormalizedEmail = model.UserName.ToUpper(),
                Name = model.Name
            };

            try
            {
                var result = await _userManager.CreateAsync(newUser, model.Password);
                if (result.Succeeded)
                {
                    // Verifica y crea los roles individualmente
                    if (!await _roleManager.RoleExistsAsync(SD.Role_Premium))
                    {
                        await _roleManager.CreateAsync(new IdentityRole(SD.Role_Premium));
                    }

                    if (!await _roleManager.RoleExistsAsync(SD.Role_Free))
                    {
                        await _roleManager.CreateAsync(new IdentityRole(SD.Role_Free));
                    }

                    if (!await _roleManager.RoleExistsAsync(SD.Role_Guest))
                    {
                        await _roleManager.CreateAsync(new IdentityRole(SD.Role_Guest));
                    }

                    if (model.Role.ToLower() == SD.Role_Premium)
                    {
                        await _userManager.AddToRoleAsync(newUser, SD.Role_Premium);
                    }
                    else
                    {
                        await _userManager.AddToRoleAsync(newUser, SD.Role_Free);
                    }

                    // Agregar categorías predeterminadas para el nuevo usuario
                    var categoriasPorDefecto = ObtenerCategoriasPorDefecto(newUser.Id);
                    // Agregar tipos cuentas predeterminados para el nuevo usuario
                    var tiposCuentasPorDefecto = ObtenerTiposCuentasPorDefecto(newUser.Id);
                    _db.Categorias.AddRange(categoriasPorDefecto);
                    _db.TiposCuentas.AddRange(tiposCuentasPorDefecto);
                    await _db.SaveChangesAsync();

                    _response.StatusCode = HttpStatusCode.OK;
                    _response.IsSuccess = true;
                    return Ok(_response);
                }
            }
            catch (Exception)
            {

            }
            _response.StatusCode = HttpStatusCode.BadRequest;
            _response.IsSuccess = false;
            _response.ErrorMessages.Add("Error mientras se registraba");
            return BadRequest(_response);

        }

        [HttpPost("invitado")]
        public async Task<IActionResult> GuestLogin()
        {
            var expirationDate = DateTime.UtcNow.AddHours(1); // Expira en 1 hora

            var guestUser = new ApplicationUser
            {
                UserName = $"invitado_{Guid.NewGuid()}",
                Email = $"invitado_{Guid.NewGuid()}@guest.com",
                Name = "Invitado",
                ExpirationDate = expirationDate
            };

            var result = await _userManager.CreateAsync(guestUser);
            if (!result.Succeeded)
            {
                _response.Result = new LoginResponseDTO();
                _response.StatusCode = HttpStatusCode.BadRequest;
                _response.IsSuccess = false;
                _response.ErrorMessages.Add("Error al crear el usuario invitado");
                return BadRequest(_response);
            }

            // Verificar y crear el rol Guest si no existe
            if (!await _roleManager.RoleExistsAsync(SD.Role_Guest))
            {
                await _roleManager.CreateAsync(new IdentityRole(SD.Role_Guest));
            }

            // Asignar rol de invitado
            await _userManager.AddToRoleAsync(guestUser, SD.Role_Guest);

            // Agregar categorías y tipos de cuentas predeterminadas para el usuario invitado
            var categoriasPorDefecto = ObtenerCategoriasPorDefecto(guestUser.Id);
            var tiposCuentasPorDefecto = ObtenerTiposCuentasPorDefecto(guestUser.Id);
            _db.Categorias.AddRange(categoriasPorDefecto);
            _db.TiposCuentas.AddRange(tiposCuentasPorDefecto);
            await _db.SaveChangesAsync();

            // Generar JWT token para el usuario invitado
            var roles = await _userManager.GetRolesAsync(guestUser);

            JwtSecurityTokenHandler tokenHandler = new();
            byte[] key = Encoding.ASCII.GetBytes(secretKey);

            SecurityTokenDescriptor tokenDescriptor = new()
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim("fullName", guestUser.Name),
                    new Claim("id", guestUser.Id.ToString()),
                    new Claim(ClaimTypes.Email, guestUser.UserName.ToString()),
                    new Claim(ClaimTypes.Role, roles.FirstOrDefault())
                }),
                Expires = DateTime.UtcNow.AddHours(1), // Tokens para invitados con menor duración
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);

            LoginResponseDTO loginResponse = new()
            {
                Email = guestUser.Email,
                Token = tokenHandler.WriteToken(token)
            };

            if (loginResponse.Email == null || string.IsNullOrEmpty(loginResponse.Token))
            {
                _response.StatusCode = HttpStatusCode.BadRequest;
                _response.IsSuccess = false;
                _response.ErrorMessages.Add("Error al crear el token para el usuario invitado");
                return BadRequest(_response);
            }

            _response.StatusCode = HttpStatusCode.OK;
            _response.IsSuccess = true;
            _response.Result = loginResponse;
            return Ok(_response);
        }

        [HttpPost("CambiarRolUsuarioFreeAPremium")]
        public async Task<IActionResult> CambiarRolUsuarioFreeAPremium()
        {
            string usuarioId = await ObtenerUsuarioId();

            if(usuarioId == null)
            {
                return Unauthorized(); // Devuelve un código de estado 401 (No autorizado)
            }

            ApplicationUser userFreeFromDb = _db.ApplicationUsers
                .FirstOrDefault(u => u.Id == usuarioId);

            if(await EsUsuarioFree(usuarioId))
            {
                await _userManager.RemoveFromRoleAsync(userFreeFromDb, SD.Role_Free);
                await _userManager.AddToRoleAsync(userFreeFromDb, SD.Role_Premium);
                _response.StatusCode = HttpStatusCode.OK;
                _response.IsSuccess = true;
                return Ok(_response);
            }

            _response.StatusCode = HttpStatusCode.BadRequest;
            _response.IsSuccess = false;
            _response.ErrorMessages.Add("Hubo un error. Por favor vuelva a intentarlo mas tarde.");
            return BadRequest(_response);
        }

        private List<Categoria> ObtenerCategoriasPorDefecto(string usuarioId)
        {
            return new List<Categoria>
            {
                new Categoria { Nombre = "Salario", TipoOperacionId = 1, ApplicationUserId = usuarioId },
                new Categoria { Nombre = "Comida", TipoOperacionId = 2, ApplicationUserId = usuarioId },
                new Categoria { Nombre = "Renta", TipoOperacionId = 2, ApplicationUserId = usuarioId }
            };
        }

        private List<TipoCuenta> ObtenerTiposCuentasPorDefecto(string usuarioId)
        {
            return new List<TipoCuenta>
            {
                new TipoCuenta { Nombre = "Efectivo", ApplicationUserId = usuarioId },
                new TipoCuenta { Nombre = "Cuentas de Banco", ApplicationUserId = usuarioId },
                new TipoCuenta { Nombre = "Tarjetas", ApplicationUserId = usuarioId },
                new TipoCuenta { Nombre = "Prestamo", ApplicationUserId = usuarioId }
            };
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
