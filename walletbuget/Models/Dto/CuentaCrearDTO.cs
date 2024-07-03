using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
namespace walletbuget.Models.Dto
{
    public class CuentaCrearDTO
    {
        [Required]
        [StringLength(50)]
        public string Nombre { get; set; }
        [Required]
        public int TipoCuenta { get; set; }
        public decimal Balance { get; set; }
        public string Descripcion { get; set; }

    }
}
