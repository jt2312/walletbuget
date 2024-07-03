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
    [Route("api/Cuenta")]
    [ApiController]
    public class CuentaController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private ApiResponse _response;
        public CuentaController(ApplicationDbContext db)
        {
            _db = db;
            _response = new ApiResponse();
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerCuentas()
        {
            string usuarioId = await ObtenerUsuarioId();

            if (usuarioId == null)
            {
                return Unauthorized(); // Devuelve un código de estado 401 (No autorizado)
            }

            //obtengo las cuentas del context
            List<Cuenta> cuentas = await _db.Cuentas.Where(c => c.ApplicationUserId == usuarioId).ToListAsync();

            //mapeo las cuentas a obj DTO
            List<CuentaMostrarDTO> cuentasMostrarDTO = cuentas.Select(c => new CuentaMostrarDTO
            {
                Id = c.Id,
                Nombre = c.Nombre,
                TipoCuenta = c.TipoCuentaId,
                Balance = c.Balance,
                Descripcion = c.Descripcion
            }).ToList();

            _response.Result = cuentasMostrarDTO;
            _response.StatusCode = HttpStatusCode.OK;

            return Ok(_response);
        }

        [HttpGet("{id:int}", Name = "ObtenerCuenta")]
        public async Task<IActionResult> ObtenerCuenta(int id)
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

            // Obtener la cuenta del usuario actual por su ID
            Cuenta cuenta = await _db.Cuentas.FirstOrDefaultAsync(u => u.Id == id && u.ApplicationUserId == usuarioId);

            if (cuenta == null)
            {
                _response.StatusCode = HttpStatusCode.NotFound;
                _response.IsSuccess = false;
                return NotFound(_response);
            }

            //mapeo las cuentas a obj DTO
            CuentaMostrarDTO cuentaMostrarDTO = new CuentaMostrarDTO
            {
                Id = cuenta.Id,
                Nombre = cuenta.Nombre,
                TipoCuenta = cuenta.TipoCuentaId,
                Balance = cuenta.Balance,
                Descripcion = cuenta.Descripcion
            };

            _response.Result = cuentaMostrarDTO;
            _response.StatusCode = HttpStatusCode.OK;

            return Ok(_response);
        }


        [HttpPost]
        public async Task<ActionResult<ApiResponse>> CrearCuenta([FromBody] CuentaCrearDTO cuentaCrearDTO)
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
                    // Añadir la nueva categoria a la base de datos
                    Cuenta cuentaCrear = new()
                    {
                        Nombre = cuentaCrearDTO.Nombre,
                        TipoCuentaId = cuentaCrearDTO.TipoCuenta,
                        ApplicationUserId = await ObtenerUsuarioId(),
                        Descripcion = cuentaCrearDTO.Descripcion,
                        Balance = cuentaCrearDTO.Balance
                    };

                    _db.Cuentas.Add(cuentaCrear);
                    await _db.SaveChangesAsync();

                    CuentaMostrarDTO cuentaCreada = new CuentaMostrarDTO
                    {
                        Id = cuentaCrear.Id,
                        Nombre = cuentaCrear.Nombre,
                        TipoCuenta = cuentaCrear.TipoCuentaId,
                        Balance = cuentaCrear.Balance,
                        Descripcion = cuentaCrear.Descripcion
                    };

                    _response.Result = cuentaCreada;
                    _response.StatusCode = HttpStatusCode.Created;
                    return CreatedAtRoute("ObtenerCuenta", new { id = cuentaCrear.Id }, _response);
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
                _response.ErrorMessages = new List<string>() { ex.ToString() };
            }

            return _response;
        }


        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse>> ActualizarCuenta(int id, [FromBody] CuentaActualizarDTO cuentaActualizarDTO)
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
                    if (cuentaActualizarDTO == null || id != cuentaActualizarDTO.Id)
                    {
                        _response.StatusCode = HttpStatusCode.BadRequest;
                        _response.IsSuccess = false;
                        return BadRequest(_response);
                    }

                    Cuenta cuentaDb = await _db.Cuentas.FirstOrDefaultAsync(c => c.Id == id && c.ApplicationUserId == usuarioId);

                    if (cuentaDb == null)
                    {
                        _response.StatusCode = HttpStatusCode.BadRequest;
                        _response.IsSuccess = false;
                        return BadRequest(_response);
                    }

                    // Verificar si hay transacciones asociadas con esta cuenta
                    var transaccionesAsociadas = await _db.Transacciones
                        .Where(t => t.CuentaId == id && t.ApplicationUserId == usuarioId)
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
                                _response.ErrorMessages = new List<string> { "No se puede actualizar una cuenta con transacciones en un mes cerrado." };
                                return BadRequest(_response);
                            }
                        }
                    }

                    cuentaDb.Nombre = cuentaActualizarDTO.Nombre;
                    cuentaDb.TipoCuentaId = cuentaActualizarDTO.TipoCuenta;
                    cuentaDb.Balance = cuentaActualizarDTO.Balance;
                    cuentaDb.Descripcion= cuentaActualizarDTO.Descripcion;

                    _db.Cuentas.Update(cuentaDb);
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
        public async Task<ActionResult<ApiResponse>> EliminarCuenta(int id)
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

                Cuenta cuentaDb = await _db.Cuentas.FirstOrDefaultAsync(c => c.Id == id && c.ApplicationUserId == usuarioId);

                if (cuentaDb == null)
                {
                    _response.StatusCode = HttpStatusCode.NotFound;
                    _response.IsSuccess = false;
                    return NotFound(_response);
                }

                // Verificar si hay transacciones asociadas con esta cuenta
                var transaccionesAsociadas = await _db.Transacciones
                    .Where(t => t.CuentaId == id && t.ApplicationUserId == usuarioId)
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
                            _response.ErrorMessages = new List<string> { "No se puede eliminar una cuenta con transacciones en un mes cerrado." };
                            return BadRequest(_response);
                        }
                    }
                }

                _db.Cuentas.RemoveRange(cuentaDb);
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
