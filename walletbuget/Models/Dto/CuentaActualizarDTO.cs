using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace walletbuget.Models.Dto
{
    public class CuentaActualizarDTO
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Nombre { get; set; }
        [Required]
        public int TipoCuenta { get; set; }
        [Required]
        public decimal Balance { get; set; }
        [Required]
        public string Descripcion { get; set; }


    }
}