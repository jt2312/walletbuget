using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace walletbuget.Models.Dto
{
    public class TransaccionActualizarDTO
    {
        public int Id { get; set; }
        [DataType(DataType.Date)]
        public DateTime FechaTransaccion { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Monto { get; set; }
        [Required]
        public int Cuenta { get; set; }
        [Required]
        public int Categoria { get; set; }
        [StringLength(1000)]
        public string Nota { get; set; }
    }
}
