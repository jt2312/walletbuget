using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace walletbuget.Models.Dto
{
    public class TransaccionMostrarDTO
    {
        public int Id { get; set; }

        [JsonConverter(typeof(DateOnlyJsonConverter))]
        public DateTime FechaTransaccion { get; set; }

        public decimal Monto { get; set; }
        public int CuentaId { get; set; }
        public int CategoriaId { get; set; }
        public string Nota { get; set; }
    }
}
