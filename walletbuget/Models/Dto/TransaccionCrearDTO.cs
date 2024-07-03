using System.ComponentModel.DataAnnotations;

namespace walletbuget.Models.Dto
{
    public class TransaccionCrearDTO
    {
        [DataType(DataType.Date)]
        public DateTime FechaTransaccion { get; set; }
        public decimal Monto { get; set; }
        public int CuentaId { get; set; }
        public int CategoriaId { get; set; }
        public string Nota { get; set; }
    }
}
