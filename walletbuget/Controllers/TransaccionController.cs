using walletbuget.Data;
using walletbuget.Models;
using walletbuget.Models.Dto;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Net;
using System.Security.Claims;

namespace walletbuget.Controllers
{
    [Route("api/Transaccion")]
    [ApiController]
    public class TransaccionController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private ApiResponse _response;
        private readonly UserManager<ApplicationUser> _userManager;

        public TransaccionController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
            _response = new ApiResponse();
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerTransacciones()
        {
            string usuarioId = await ObtenerUsuarioId();

            if (usuarioId == null)
            {
                return Unauthorized(); // Devuelve un código de estado 401 (No autorizado)
            }

            List<Transaccion> transacciones = await _db.Transacciones.Where(t => t.ApplicationUserId == usuarioId).ToListAsync();

            List<TransaccionMostrarDTO> transaccionMostrarDTO = transacciones.Select(t => new TransaccionMostrarDTO
            {
                Id = t.Id,
                FechaTransaccion = t.FechaTransaccion.Date,  // Solo la fecha
                Monto = t.Monto,
                CuentaId = t.CuentaId,
                CategoriaId = t.CategoriaId,
                Nota = t.Nota
            }).ToList();

            _response.Result = transaccionMostrarDTO;
            _response.StatusCode = HttpStatusCode.OK;

            return Ok(_response);
        }

        [HttpGet("{id:int}",Name = "ObtenerTransaccion")]
        public async Task<ActionResult> ObtenerTransaccion(int id)
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

            Transaccion transaccion = await _db.Transacciones.FirstOrDefaultAsync(t => t.Id == id && t.ApplicationUserId == usuarioId);

            if (transaccion == null)
            {
                _response.StatusCode = HttpStatusCode.NotFound;
                _response.IsSuccess = false;
                return NotFound(_response);
            }

            TransaccionMostrarDTO transaccionMostrarDTO = new TransaccionMostrarDTO
            {
                Id = transaccion.Id,
                FechaTransaccion = transaccion.FechaTransaccion.Date,
                Monto = transaccion.Monto,
                CuentaId = transaccion.CuentaId,
                CategoriaId = transaccion.CategoriaId,
                Nota = transaccion.Nota
            };

            _response.Result = transaccionMostrarDTO;
            _response.StatusCode = HttpStatusCode.OK;

            return Ok(_response);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse>> CrearTransaccion([FromBody]TransaccionCrearDTO transaccionCrearDTO)
        {
            string usuarioId = await ObtenerUsuarioId();

            if (usuarioId == null)
            {
                return Unauthorized(); // Devuelve un código de estado 401 (No autorizado)
            }

            if (await EsMesCerrado(usuarioId, transaccionCrearDTO.FechaTransaccion))
            {
                _response.IsSuccess = false;
                _response.StatusCode = HttpStatusCode.BadRequest;
                _response.ErrorMessages = new List<string> { "No se pueden agregar transacciones a un mes cerrado." };
                return BadRequest(_response);
            }

            try
            {
                if (ModelState.IsValid)
                {
                    var cuenta = await _db.Cuentas.FirstOrDefaultAsync(c => c.Id == transaccionCrearDTO.CuentaId && c.ApplicationUserId == usuarioId);
                    if (cuenta == null)
                    {
                        _response.StatusCode = HttpStatusCode.BadRequest;
                        _response.IsSuccess = false;
                        _response.ErrorMessages = new List<string> { "La cuenta no existe o no pertenece al usuario actual." };
                        return BadRequest(_response);
                    }

                    Transaccion transaccionCrear = new()
                    {
                        FechaTransaccion = transaccionCrearDTO.FechaTransaccion.Date,
                        Monto = transaccionCrearDTO.Monto,
                        CuentaId = transaccionCrearDTO.CuentaId,
                        CategoriaId = transaccionCrearDTO.CategoriaId,
                        Nota = transaccionCrearDTO.Nota,
                        ApplicationUserId = await ObtenerUsuarioId()
                    };

                    _db.Transacciones.Add(transaccionCrear);

                    if (await EsIngreso(transaccionCrearDTO.CategoriaId))
                    {
                        cuenta.Balance += transaccionCrear.Monto;
                    }
                    else
                    {
                        cuenta.Balance -= transaccionCrear.Monto;
                    }

                    _db.Cuentas.Update(cuenta);
                    await _db.SaveChangesAsync();

                    TransaccionMostrarDTO transaccionCreada = new TransaccionMostrarDTO
                    {
                        Id = transaccionCrear.Id,
                        FechaTransaccion = transaccionCrear.FechaTransaccion.Date,
                        Monto = transaccionCrear.Monto,
                        CuentaId = transaccionCrear.CuentaId,
                        CategoriaId = transaccionCrear.CategoriaId,
                        Nota = transaccionCrear.Nota
                    };

                    _response.Result = transaccionCreada;
                    _response.StatusCode = HttpStatusCode.Created;
                    return CreatedAtRoute("ObtenerTransaccion", new { id = transaccionCrear.Id }, _response);
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
        public async Task<ActionResult<ApiResponse>> ActualizarTransaccion(int id, [FromBody]TransaccionActualizarDTO transaccionActualizarDTO)
        {
            string usuarioId = await ObtenerUsuarioId();

            if (usuarioId == null)
            {
                return Unauthorized(); // Devuelve un código de estado 401 (No autorizado)
            }

            if (await EsMesCerrado(usuarioId, transaccionActualizarDTO.FechaTransaccion))
            {
                _response.IsSuccess = false;
                _response.StatusCode = HttpStatusCode.BadRequest;
                _response.ErrorMessages = new List<string> { "No se pueden actualizar transacciones de un mes cerrado." };
                return BadRequest(_response);
            }

            try
            {
                if (ModelState.IsValid)
                {
                    if(transaccionActualizarDTO == null || id != transaccionActualizarDTO.Id)
                    {
                        _response.StatusCode = HttpStatusCode.BadRequest;
                        _response.IsSuccess = false;
                        return BadRequest(_response);
                    }

                    Transaccion transaccionDeLaDb = await _db.Transacciones.FirstOrDefaultAsync(t => t.Id == id && t.ApplicationUserId == usuarioId);
                    if (transaccionDeLaDb == null)
                    {
                        _response.StatusCode = HttpStatusCode.BadRequest;
                        _response.IsSuccess = false;
                        return BadRequest(_response);
                    }

                    var cuenta = await _db.Cuentas.FirstOrDefaultAsync(c => c.Id == transaccionDeLaDb.CuentaId && c.ApplicationUserId == usuarioId);
                    if (cuenta == null)
                    {
                        _response.StatusCode = HttpStatusCode.BadRequest;
                        _response.IsSuccess = false;
                        _response.ErrorMessages = new List<string> { "La cuenta no existe o no pertenece al usuario actual." };
                        return BadRequest(_response);
                    }

                    // Revertir el balance anterior
                    if (await EsIngreso(transaccionDeLaDb.CategoriaId))
                    {
                        cuenta.Balance -= transaccionDeLaDb.Monto;
                    }
                    else
                    {
                        cuenta.Balance += transaccionDeLaDb.Monto;
                    }

                    transaccionDeLaDb.FechaTransaccion = transaccionActualizarDTO.FechaTransaccion.Date;
                    transaccionDeLaDb.Monto = transaccionActualizarDTO.Monto;
                    transaccionDeLaDb.CuentaId = transaccionActualizarDTO.Cuenta;
                    transaccionDeLaDb.CategoriaId = transaccionActualizarDTO.Categoria;
                    transaccionDeLaDb.Nota = transaccionActualizarDTO.Nota;

                    // Aplicar el nuevo monto
                    if (await EsIngreso(transaccionActualizarDTO.Categoria))
                    {
                        cuenta.Balance += transaccionActualizarDTO.Monto;
                    }
                    else
                    {
                        cuenta.Balance -= transaccionActualizarDTO.Monto;
                    }

                    _db.Transacciones.Update(transaccionDeLaDb);
                    _db.Cuentas.Update(cuenta);
                    await _db.SaveChangesAsync();

                    _response.StatusCode = HttpStatusCode.NoContent;
                    return Ok(_response);
                }
                else
                {
                    _response.IsSuccess = false;
                }
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string>() { ex.Message };
            }

            return BadRequest(_response);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> EliminarTransaccion(int id)
        {
            string usuarioId = await ObtenerUsuarioId();

            if (usuarioId == null)
            {
                return Unauthorized(); // Devuelve un código de estado 401 (No autorizado)
            }

            try
            {
                if(id == 0)
                {
                    _response.StatusCode = HttpStatusCode.BadRequest;
                    _response.IsSuccess = false;
                    return BadRequest();
                }

                Transaccion transaccionDeLaDb = await _db.Transacciones.FirstOrDefaultAsync(t => t.Id == id && t.ApplicationUserId == usuarioId);

                if (transaccionDeLaDb == null)
                {
                    _response.StatusCode = HttpStatusCode.BadRequest;
                    _response.IsSuccess = false;
                    return BadRequest();
                }

                if (await EsMesCerrado(usuarioId, transaccionDeLaDb.FechaTransaccion))
                {
                    _response.IsSuccess = false;
                    _response.StatusCode = HttpStatusCode.BadRequest;
                    _response.ErrorMessages = new List<string> { "No se pueden eliminar transacciones de un mes cerrado." };
                    return BadRequest(_response);
                }

                var cuenta = await _db.Cuentas.FirstOrDefaultAsync(c => c.Id == transaccionDeLaDb.CuentaId && c.ApplicationUserId == usuarioId);
                if (cuenta == null)
                {
                    _response.StatusCode = HttpStatusCode.BadRequest;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "La cuenta no existe o no pertenece al usuario actual." };
                    return BadRequest(_response);
                }

                // Revertir el balance anterior
                if (await EsIngreso(transaccionDeLaDb.CategoriaId))
                {
                    cuenta.Balance -= transaccionDeLaDb.Monto;
                }
                else
                {
                    cuenta.Balance += transaccionDeLaDb.Monto;
                }

                _db.Transacciones.RemoveRange(transaccionDeLaDb);
                _db.Cuentas.Update(cuenta);
                await _db.SaveChangesAsync();

                _response.StatusCode = HttpStatusCode.NoContent;
                return Ok(_response);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string>() { ex.ToString() };
            }

            return BadRequest(_response);
        }

        [HttpPost("CerrarMes")]
        public async Task<ActionResult<ApiResponse>> CerrarMes([FromBody] CerrarMesDTO cerrarMesDto)
        {
            string usuarioId = await ObtenerUsuarioId();

            if (usuarioId == null)
            {
                return Unauthorized(); // Devuelve un código de estado 401 (No autorizado)
            }

            if (await EsUsuarioFree(usuarioId))
            {
                _response.IsSuccess = false;
                _response.StatusCode = HttpStatusCode.BadRequest;
                _response.ErrorMessages = new List<string> { "Los usuarios Free no pueden cerrar el mes." };
                return BadRequest(_response);
            }

            var mesCerradoExistente = await _db.MesesCerrados
                .FirstOrDefaultAsync(mc => mc.ApplicationUserId == usuarioId && mc.Año == cerrarMesDto.Año && mc.Mes == cerrarMesDto.Mes);

            if (mesCerradoExistente != null)
            {
                _response.IsSuccess = false;
                _response.StatusCode = HttpStatusCode.BadRequest;
                _response.ErrorMessages = new List<string> { "El mes ya está cerrado." };
                return BadRequest(_response);
            }

            MesCerrado nuevoMesCerrado = new MesCerrado
            {
                ApplicationUserId = usuarioId,
                Año = cerrarMesDto.Año,
                Mes = cerrarMesDto.Mes
            };

            _db.MesesCerrados.Add(nuevoMesCerrado);
            await _db.SaveChangesAsync();

            _response.IsSuccess = true;
            _response.StatusCode = HttpStatusCode.OK;
            return Ok(_response);
        }

        private async Task<bool> EsMesCerrado(string usuarioId, DateTime fecha)
        {
            var mesCerrado = await _db.MesesCerrados
                .FirstOrDefaultAsync(mc => mc.ApplicationUserId == usuarioId && mc.Año == fecha.Year && mc.Mes == fecha.Month);

            return mesCerrado != null;
        }

        [HttpGet("ExportarTodoExcel")]
        public async Task<ActionResult> ExportarExcel()
        {
            string usuarioId = await ObtenerUsuarioId();
            if (usuarioId == null)
            {
                return Unauthorized(); // Devuelve un código de estado 401 (No autorizado)
            }

            var _informacionTransacciones = await ObtenerInformacionTransacciones(usuarioId);

            using(XLWorkbook wb = new XLWorkbook())
            {
                wb.AddWorksheet(_informacionTransacciones, "Usuario Transacciones");
                using(MemoryStream ms = new MemoryStream())
                {
                    wb.SaveAs(ms);
                    return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Presupuesto.xlsx");
                }
            }
        }

        [HttpGet("ExportarExcelPorSemana")]
        public async Task<ActionResult> ExportarExcelPorSemana()
        {
            string usuarioId = await ObtenerUsuarioId();
            if (usuarioId == null)
            {
                return Unauthorized();
            }

            var inicioSemana = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
            var finSemana = inicioSemana.AddDays(7);

            var _informacionTransacciones = await ObtenerInformacionTransaccionesPorRango(usuarioId, inicioSemana, finSemana);

            using (XLWorkbook wb = new XLWorkbook())
            {
                wb.AddWorksheet(_informacionTransacciones, "Transacciones Semana");
                using (MemoryStream ms = new MemoryStream())
                {
                    wb.SaveAs(ms);
                    return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Transacciones_Semana.xlsx");
                }
            }
        }

        [HttpGet("ExportarExcelPorMes")]
        public async Task<ActionResult> ExportarExcelPorMes()
        {
            string usuarioId = await ObtenerUsuarioId();
            if (usuarioId == null)
            {
                return Unauthorized();
            }

            var inicioMes = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var finMes = inicioMes.AddMonths(1);

            var _informacionTransacciones = await ObtenerInformacionTransaccionesPorRango(usuarioId, inicioMes, finMes);

            using (XLWorkbook wb = new XLWorkbook())
            {
                wb.AddWorksheet(_informacionTransacciones, "Transacciones Mes");
                using (MemoryStream ms = new MemoryStream())
                {
                    wb.SaveAs(ms);
                    return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Transacciones_Mes.xlsx");
                }
            }
        }

        private async Task<DataTable> ObtenerInformacionTransaccionesPorRango(string usuarioId, DateTime inicio, DateTime fin)
        {
            DataTable dt = new DataTable();
            dt.TableName = "InformacionTransacciones";
            dt.Columns.Add("FechaTransaccion", typeof(DateTime));
            dt.Columns.Add("Cuenta", typeof(string));
            dt.Columns.Add("Monto", typeof(decimal));
            dt.Columns.Add("TipoOperacion", typeof(string));
            dt.Columns.Add("Nota", typeof(string));

            var _list = await _db.Transacciones
                .Where(t => t.ApplicationUserId == usuarioId && t.FechaTransaccion >= inicio && t.FechaTransaccion < fin)
                .Include(t => t.Cuenta)
                .Include(t => t.Categoria)
                    .ThenInclude(c => c.TipoOperacion)
                .ToListAsync();

            if (_list.Count > 0)
            {
                _list.ForEach(item =>
                {
                    dt.Rows.Add(item.FechaTransaccion, item.Cuenta.Nombre, item.Monto, item.Categoria.TipoOperacion.Descripcion, item.Nota);
                });
            }

            return dt;
        }

        private async Task<DataTable> ObtenerInformacionTransacciones(string usuarioId)
        {
            DataTable dt = new DataTable();
            dt.TableName = "InformacionTransacciones";
            dt.Columns.Add("FechaTransaccion", typeof(DateTime));
            dt.Columns.Add("Cuenta", typeof(string));
            dt.Columns.Add("Monto", typeof(decimal));
            dt.Columns.Add("TipoOperacion", typeof(string));
            dt.Columns.Add("Nota", typeof(string));

            var _list = await _db.Transacciones
                .Where(t => t.ApplicationUserId == usuarioId)
                .Include(t => t.Cuenta)
                .Include(t => t.Categoria)
                    .ThenInclude(c => c.TipoOperacion) // Incluimos TipoOperacion
                .ToListAsync();

            if (_list.Count > 0 )
            {
                _list.ForEach(item =>
                {
                    dt.Rows.Add(item.FechaTransaccion, item.Cuenta.Nombre, item.Monto, item.Categoria.TipoOperacion.Descripcion, item.Nota);
                });
            }

            return dt;
        }

        [HttpGet("ObtenerTransaccionesDeLaSemana")]
        public async Task<IActionResult> ObtenerTransaccionesDeLaSemana()
        {
            string usuarioId = await ObtenerUsuarioId();

            if (usuarioId == null)
            {
                return Unauthorized(); // Devuelve un código de estado 401 (No autorizado)
            }

            var inicioSemana = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
            var finSemana = inicioSemana.AddDays(7);

            List<Transaccion> transacciones = await _db.Transacciones
                .Where(t => t.ApplicationUserId == usuarioId && t.FechaTransaccion >= inicioSemana && t.FechaTransaccion < finSemana)
                .ToListAsync();

            List<TransaccionMostrarDTO> transaccionMostrarDTO = transacciones.Select(t => new TransaccionMostrarDTO
            {
                Id = t.Id,
                FechaTransaccion = t.FechaTransaccion.Date,
                Monto = t.Monto,
                CuentaId = t.CuentaId,
                CategoriaId = t.CategoriaId,
                Nota = t.Nota
            }).ToList();

            _response.Result = transaccionMostrarDTO;
            _response.StatusCode = HttpStatusCode.OK;

            return Ok(_response);
        }

        [HttpGet("ObtenerTransaccionesDelMes")]
        public async Task<IActionResult> ObtenerTransaccionesDelMes()
        {
            string usuarioId = await ObtenerUsuarioId();

            if (usuarioId == null)
            {
                return Unauthorized(); // Devuelve un código de estado 401 (No autorizado)
            }

            var inicioMes = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var finMes = inicioMes.AddMonths(1);

            List<Transaccion> transacciones = await _db.Transacciones
                .Where(t => t.ApplicationUserId == usuarioId && t.FechaTransaccion >= inicioMes && t.FechaTransaccion < finMes)
                .ToListAsync();

            List<TransaccionMostrarDTO> transaccionMostrarDTO = transacciones.Select(t => new TransaccionMostrarDTO
            {
                Id = t.Id,
                FechaTransaccion = t.FechaTransaccion.Date,
                Monto = t.Monto,
                CuentaId = t.CuentaId,
                CategoriaId = t.CategoriaId,
                Nota = t.Nota
            }).ToList();

            _response.Result = transaccionMostrarDTO;
            _response.StatusCode = HttpStatusCode.OK;

            return Ok(_response);
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

        private async Task<bool> EsIngreso(int categoriaId)
        {
            var categoria = await _db.Categorias.Include(c => c.TipoOperacion)
                                                .FirstOrDefaultAsync(c => c.Id == categoriaId);
            return categoria?.TipoOperacion?.Descripcion == "Ingresos";
        }
    }
}
