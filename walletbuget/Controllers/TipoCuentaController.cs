using walletbuget.Data;
using walletbuget.Models;
using walletbuget.Models.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Security.Claims;

namespace walletbuget.Controllers
{
    [Route("api/TipoCuenta")]
    [ApiController]
    public class TipoCuentaController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private ApiResponse _response;

        public TipoCuentaController(ApplicationDbContext db)
        {
            _db = db;
            _response = new ApiResponse();
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerTiposCuentas()
        {
            string usuarioId = await ObtenerUsuarioId();

            if (usuarioId == null)
            {
                return Unauthorized(); // Devuelve un código de estado 401 (No autorizado)
            }

            List<TipoCuenta> tiposCuentas = await _db.TiposCuentas.Where(c => c.ApplicationUserId == usuarioId).ToListAsync();

            List<TipoCuentaMostrarDTO> TipoCuentaMostrarDTO = tiposCuentas.Select(c => new TipoCuentaMostrarDTO
            {
                Id = c.Id,
                Nombre = c.Nombre,
            }).ToList();

            _response.Result = TipoCuentaMostrarDTO;
            _response.StatusCode = HttpStatusCode.OK;

            return Ok(_response);
        }

        [HttpGet("{id:int}", Name = "ObtenerTipoCuenta")]
        public async Task<IActionResult> ObtenerTipoCuenta(int id)
        {
            string usuarioId = await ObtenerUsuarioId();

            if (usuarioId == null)
            {
                return Unauthorized(); // Devuelve un código de estado 401 (No autorizado)
            }

            if (id == 0)
            {
                _response.StatusCode = HttpStatusCode.BadRequest;
                _response.IsSuccess = false;
                return BadRequest(_response);
            }

            TipoCuenta tipoCuenta = await _db.TiposCuentas.FirstOrDefaultAsync(t => t.Id == id && t.ApplicationUserId == usuarioId);

            if (tipoCuenta == null)
            {
                _response.StatusCode = HttpStatusCode.NotFound;
                _response.IsSuccess = false;
                return NotFound(_response);
            }

            TipoCuentaMostrarDTO tipoCuentaMostrarDTO = new TipoCuentaMostrarDTO
            {
                Id = tipoCuenta.Id,
                Nombre = tipoCuenta.Nombre
            };

            _response.Result = tipoCuentaMostrarDTO;
            _response.StatusCode = HttpStatusCode.OK;

            return Ok(_response);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse>> CrearTipoCuenta([FromBody] TipoCuentaCrearDTO tipoCuentaCrearDTO)
        {
            string usuarioId = await ObtenerUsuarioId();

            if (usuarioId == null)
            {
                return Unauthorized(); // Devuelve un código de estado 401 (No autorizado)
            }

            try
            {
                if (ModelState.IsValid)
                {
                    TipoCuenta tipoCuentaCrear = new()
                    {
                        Nombre = tipoCuentaCrearDTO.Nombre,
                        ApplicationUserId = await ObtenerUsuarioId()
                    };

                    //Verificar si ya existe un tipo cuenta con el mismo nombre para el usuario
                    if(await _db.TiposCuentas.AnyAsync(t => t.Nombre == tipoCuentaCrear.Nombre && t.ApplicationUserId == usuarioId))
                    {
                        _response.IsSuccess = false;
                        _response.StatusCode = HttpStatusCode.BadRequest;
                        _response.ErrorMessages = new List<string> { "Ya existe un tipo cuenta con este nombre" };
                        return BadRequest(_response);
                    }

                    _db.TiposCuentas.Add(tipoCuentaCrear);
                    await _db.SaveChangesAsync();

                    TipoCuentaMostrarDTO tipocuentaCreada = new TipoCuentaMostrarDTO
                    {
                        Id = tipoCuentaCrear.Id,
                        Nombre = tipoCuentaCrear.Nombre
                    };

                    _response.Result = tipocuentaCreada;
                    _response.StatusCode = HttpStatusCode.Created;
                    return CreatedAtRoute("ObtenerTipoCuenta", new { id = tipoCuentaCrear.Id }, _response);
                }
                else
                {
                    _response.IsSuccess = false;
                    _response.StatusCode = HttpStatusCode.BadRequest;
                }
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.StatusCode = HttpStatusCode.InternalServerError;
                _response.ErrorMessages = new List<string>() { ex.ToString() };
            }

            return _response;
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse>> ActualizarTipoCuenta(int id, [FromBody] TipoCuentaActualizarDTO tipoCuentaActualizarDTO)
        {
            string usuarioId = await ObtenerUsuarioId();

            if (usuarioId == null)
            {
                return Unauthorized(); // Devuelve un código de estado 401 (No autorizado)
            }

            try
            {
                if (ModelState.IsValid)
                {
                    if (tipoCuentaActualizarDTO == null || id != tipoCuentaActualizarDTO.Id)
                    {
                        _response.StatusCode = HttpStatusCode.BadRequest;
                        _response.IsSuccess = false;
                        return BadRequest(_response);
                    }

                    TipoCuenta tipoCuentaDeLaDb = await _db.TiposCuentas.FirstOrDefaultAsync(t => t.Id == id && t.ApplicationUserId == usuarioId); 

                    if (tipoCuentaDeLaDb == null)
                    {
                        _response.StatusCode = HttpStatusCode.BadRequest;
                        _response.IsSuccess = false;
                        return BadRequest(_response);
                    }

                    // Verificar si hay cuentas asociadas con este tipo de cuenta
                    var cuentasAsociadas = await _db.Cuentas
                        .Where(c => c.TipoCuentaId == id && c.ApplicationUserId == usuarioId)
                        .ToListAsync();

                    foreach (var cuenta in cuentasAsociadas)
                    {
                        // Verificar si hay transacciones asociadas con esta cuenta
                        var transaccionesAsociadas = await _db.Transacciones
                            .Where(t => t.CuentaId == cuenta.Id && t.ApplicationUserId == usuarioId)
                            .ToListAsync();

                        if (transaccionesAsociadas.Any())
                        {
                            // Verificar si alguna de estas transacciones pertenece a un mes cerrado
                            foreach (var transaccion in transaccionesAsociadas)
                            {
                                if (await EsMesCerrado(usuarioId, transaccion.FechaTransaccion))
                                {
                                    _response.IsSuccess = false;
                                    _response.StatusCode = HttpStatusCode.Forbidden;
                                    _response.ErrorMessages = new List<string> { "No se puede actualizar un tipo de cuenta con transacciones en un mes cerrado." };
                                    return BadRequest(_response);
                                }
                            }
                        }
                    }

                    tipoCuentaDeLaDb.Nombre = tipoCuentaActualizarDTO.Nombre;

                    _db.TiposCuentas.Update(tipoCuentaDeLaDb);
                    await _db.SaveChangesAsync();
                    _response.StatusCode = HttpStatusCode.NoContent;
                    return Ok(_response);
                }
                else
                {
                    _response.IsSuccess = false;
                    _response.StatusCode = HttpStatusCode.BadRequest;
                }
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.StatusCode = HttpStatusCode.InternalServerError;
                _response.ErrorMessages = new List<string>() { ex.Message };
            }

            return BadRequest(_response);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse>> EliminarTipoCuenta(int id)
        {
            string usuarioId = await ObtenerUsuarioId();

            if (usuarioId == null)
            {
                return Unauthorized(); // Devuelve un código de estado 401 (No autorizado)
            }

            try
            {
                if (id == 0)
                {
                    _response.StatusCode = HttpStatusCode.BadRequest;
                    _response.IsSuccess = false;
                    return BadRequest(_response);
                }

                TipoCuenta tipoCuentaDeLaDb = await _db.TiposCuentas.FirstOrDefaultAsync(t => t.Id == id && t.ApplicationUserId == usuarioId);

                if (tipoCuentaDeLaDb == null)
                {
                    _response.StatusCode = HttpStatusCode.BadRequest;
                    _response.IsSuccess = false;
                    return BadRequest(_response);
                }

                // Verificar si hay cuentas asociadas con este tipo de cuenta
                var cuentasAsociadas = await _db.Cuentas
                    .Where(c => c.TipoCuentaId == id && c.ApplicationUserId == usuarioId)
                    .ToListAsync();

                foreach (var cuenta in cuentasAsociadas)
                {
                    // Verificar si hay transacciones asociadas con esta cuenta
                    var transaccionesAsociadas = await _db.Transacciones
                        .Where(t => t.CuentaId == cuenta.Id && t.ApplicationUserId == usuarioId)
                        .ToListAsync();

                    if (transaccionesAsociadas.Any())
                    {
                        // Verificar si alguna de estas transacciones pertenece a un mes cerrado
                        foreach (var transaccion in transaccionesAsociadas)
                        {
                            if (await EsMesCerrado(usuarioId, transaccion.FechaTransaccion))
                            {
                                _response.IsSuccess = false;
                                _response.StatusCode = HttpStatusCode.Forbidden;
                                _response.ErrorMessages = new List<string> { "No se puede eliminar un tipo de cuenta con transacciones en un mes cerrado." };
                                return BadRequest(_response);
                            }
                        }
                    }
                }

                _db.TiposCuentas.RemoveRange(tipoCuentaDeLaDb);
                await _db.SaveChangesAsync();
                _response.StatusCode = HttpStatusCode.NoContent;
                return Ok(_response);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.StatusCode = HttpStatusCode.InternalServerError;
                _response.ErrorMessages = new List<string>() { ex.Message };
            }

            return BadRequest(_response);
        }

        private async Task<string> ObtenerUsuarioId()
        {
            string idActual = HttpContext.User.FindFirstValue("id");
            return idActual;
        }

        private async Task<bool> EsMesCerrado(string usuarioId, DateTime fecha)
        {
            var mesCerrado = await _db.MesesCerrados
                .FirstOrDefaultAsync(mc => mc.ApplicationUserId == usuarioId && mc.Año == fecha.Year && mc.Mes == fecha.Month);

            return mesCerrado != null;
        }
    }
}