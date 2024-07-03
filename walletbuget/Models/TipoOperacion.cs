using System.ComponentModel.DataAnnotations;

namespace walletbuget.Models
{
    public class TipoOperacion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Descripcion { get; set; }

        public ICollection<Categoria> Categorias { get; set; }
    }
}
