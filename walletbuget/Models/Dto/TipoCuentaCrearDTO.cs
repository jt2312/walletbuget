using System.ComponentModel.DataAnnotations;

namespace walletbuget.Models.Dto
{
    public class TipoCuentaCrearDTO
    {
        [Required]
        [StringLength(250)]
        public string Nombre { get; set; }

    }
}
