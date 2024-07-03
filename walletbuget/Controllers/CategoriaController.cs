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
    [Route("api/Categoria")]
    [ApiController]
    public class CategoriaController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private ApiResponse _response;

        public CategoriaController(ApplicationDbContext db)
        {
            _db = db;
            _response = new ApiResponse();
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerCategorias()
        {
            string usuarioId = await ObtenerUsuarioId();

            if (usuarioId == null)
            {
                return Unauthorized(); // Devuelve un código de estado 401 (No autorizado)
            }

            // Obtener las categorías de la base de datos
            List<Categoria> categorias = await _db.Categorias.Where(c=>c.ApplicationUserId == usuarioId).ToListAsync();

            // Mapear las categorías a objetos DTO
            List<CategoriaMostrarDTO> categoriaMostrarDTO = categorias.Select(c => new CategoriaMostrarDTO
            {
                Id = c.Id,
                Nombre = c.Nombre,
                TipoOperacion = c.TipoOperacionId
            }).ToList();

            _response.Result = categoriaMostrarDTO;
            _response.StatusCode = HttpStatusCode.OK;

            return Ok(_response);
        }

        [HttpGet("{id:int}",Name = "ObtenerCategoria")]
        public async Task<IActionResult> ObtenerCategoria(int id)
        {
            string usuarioId = await ObtenerUsuarioId();

            if (usuarioId == null)
            {
                return Unauthorized(); // Devuelve un código de estado 401
            }

            if (id == 0)
            {
                _response.StatusCode = HttpStatusCode.BadRequest;
                _response.IsSuccess = false;
                return BadRequest(_response);
            }

            // Obtener la categoría de la base de datos
            Categoria categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.Id == id && c.ApplicationUserId == usuarioId);

            if (categoria == null)
            {
                _response.StatusCode = HttpStatusCode.NotFound;
                _response.IsSuccess = false;
                return NotFound(_response);
            }

            // Mapear la categoría a un objeto DTO
            CategoriaMostrarDTO categoriaMostrarDTO = new CategoriaMostrarDTO
            {
                Id = categoria.Id,
                Nombre = categoria.Nombre,
                TipoOperacion = categoria.TipoOperacionId
            };

            _response.Result = categoriaMostrarDTO;
            _response.StatusCode = HttpStatusCode.OK;

            return Ok(_response);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse>> CrearCategoria([FromBody] CategoriaCrearDTO categoriaCrearDTO)
        {
            string usuarioId = await ObtenerUsuarioId();
            if (usuarioId == null)
            {
                return Unauthorized(); // Devuelve un código de estado 401
            }

            if (await EsMesCerrado(usuarioId, DateTime.Now))
            {
                _response.IsSuccess = false;
                _response.StatusCode = HttpStatusCode.Forbidden;
                _response.ErrorMessages = new List<string> { "No se puede modificar los datos de un mes cerrado." };
                return BadRequest(_response);
            }

            try
            {
                if (ModelState.IsValid)
                {
                    // Añadir la nueva categoria a la base de datos
                    Categoria categoriaCrear = new()
                    {
                        Nombre = categoriaCrearDTO.Nombre,
                        TipoOperacionId = categoriaCrearDTO.TipoOperacion,
                        ApplicationUserId = await ObtenerUsuarioId()
                    };

                    //Verificar si ya existe una categoria con el mismo nombre para el usuario
                    if(await _db.Categorias.AnyAsync(c => c.Nombre == categoriaCrear.Nombre && c.ApplicationUserId == usuarioId))
                    {
                        _response.IsSuccess = false;
                        _response.StatusCode = HttpStatusCode.BadRequest;
                        _response.ErrorMessages = new List<string>() { "Ya existe una categoria con este nombre." };
                        return BadRequest(_response);
                    }

                    _db.Categorias.Add(categoriaCrear);
                    await _db.SaveChangesAsync();

                    CategoriaMostrarDTO categoriaCreada = new CategoriaMostrarDTO
                    {
                        Id = categoriaCrear.Id,
                        Nombre = categoriaCrear.Nombre,
                        TipoOperacion = categoriaCrear.TipoOperacionId
                    };

                    _response.Result = categoriaCreada;
                    _response.StatusCode = HttpStatusCode.Created;
                    return CreatedAtRoute("ObtenerCategoria", new { id = categoriaCrear.Id }, _response);
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
        public async Task<ActionResult<ApiResponse>> ActualizarCategoria(int id, [FromBody] CategoriaActualizarDTO categoriaActualizarDTO)
        {
            string usuarioId = await ObtenerUsuarioId();
            if (usuarioId == null)
            {
                return Unauthorized(); // Devuelve un código de estado 401
            }

            try
            {
                if (ModelState.IsValid)
                {
                    if(categoriaActualizarDTO == null || id != categoriaActualizarDTO.Id)
                    {
                        _response.StatusCode = HttpStatusCode.BadRequest;
                        _response.IsSuccess = false;
                        return BadRequest(_response);
                    }

                    Categoria categoriaDeLaDb = await _db.Categorias.FirstOrDefaultAsync(c => c.Id == id && c.ApplicationUserId == usuarioId);

                    if(categoriaDeLaDb == null)
                    {
                        _response.StatusCode = HttpStatusCode.NotFound;
                        _response.IsSuccess = false;
                        return BadRequest(_response);
                    }

                    // Verificar si hay transacciones asociadas con esta categoría
                    var transaccionesAsociadas = await _db.Transacciones
                        .Where(t => t.CategoriaId == id && t.ApplicationUserId == usuarioId)
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
                                _response.ErrorMessages = new List<string> { "No se puede actualizar una categoría con transacciones en un mes cerrado." };
                                return BadRequest(_response);
                            }
                        }
                    }

                    categoriaDeLaDb.Nombre = categoriaActualizarDTO.Nombre;
                    categoriaDeLaDb.TipoOperacionId = categoriaActualizarDTO.TipoOperacion;

                    _db.Categorias.Update(categoriaDeLaDb);
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
        public async Task<ActionResult<ApiResponse>> EliminarCategoria(int id)
        {
            string usuarioId = await ObtenerUsuarioId();
            if (usuarioId == null)
            {
                return Unauthorized(); // Devuelve un código de estado 401
            }

            try
            {
                if (id == 0)
                {
                    _response.StatusCode = HttpStatusCode.BadRequest;
                    _response.IsSuccess = false;
                    return BadRequest(_response);
                }

                Categoria categoriaDeLaDb = await _db.Categorias.FirstOrDefaultAsync(c => c.Id == id && c.ApplicationUserId == usuarioId);
                if (categoriaDeLaDb == null)
                {
                    _response.StatusCode = HttpStatusCode.NotFound;
                    _response.IsSuccess = false;
                    return NotFound(_response);
                }

                // Verificar si hay transacciones asociadas con esta categoría
                var transaccionesAsociadas = await _db.Transacciones
                    .Where(t => t.CategoriaId == id && t.ApplicationUserId == usuarioId)
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
                            _response.ErrorMessages = new List<string> { "No se puede eliminar una categoría con transacciones en un mes cerrado." };
                            return BadRequest(_response);
                        }
                    }
                }

                _db.Categorias.RemoveRange(categoriaDeLaDb);
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
