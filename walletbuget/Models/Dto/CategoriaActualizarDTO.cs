using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace walletbuget.Models.Dto
{
    public class CategoriaActualizarDTO
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Nombre { get; set; }
        [Required]
        public int TipoOperacion { get; set; }

    }
}
