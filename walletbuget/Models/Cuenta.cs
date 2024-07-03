using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace walletbuget.Models
{
    public class Cuenta
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Nombre { get; set; }

        [ForeignKey("TipoCuenta")]
        public int TipoCuentaId { get; set; }
        public TipoCuenta TipoCuenta { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Balance { get; set; }

        [StringLength(1000)]
        public string Descripcion { get; set; }

        public ICollection<Transaccion> Transacciones { get; set; }

        public string ApplicationUserId { get; set; }
    }
}
