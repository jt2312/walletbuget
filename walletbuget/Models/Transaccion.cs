using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace walletbuget.Models
{
    public class Transaccion
    {
        [Key]
        public int Id { get; set; }

        [DataType(DataType.Date)]
        public DateTime FechaTransaccion { get; set; } = DateTime.Today;

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Monto { get; set; }

        [StringLength(1000)]
        public string Nota { get; set; }

        [ForeignKey("Cuenta")]
        public int CuentaId { get; set; }
        public Cuenta Cuenta { get; set; }

        [ForeignKey("Categoria")]
        public int CategoriaId { get; set; }
        public Categoria Categoria { get; set; }

        [ForeignKey("ApplicationUser")]
        public string ApplicationUserId { get; set; }
        public ApplicationUser ApplicationUser { get; set; }
    }
}
