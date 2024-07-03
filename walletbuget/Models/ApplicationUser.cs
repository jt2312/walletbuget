using Microsoft.AspNetCore.Identity;

namespace walletbuget.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string Name { get; set; }

        public DateTime? ExpirationDate { get; set; } // Nuevo campo para usuarios invitados

        public ICollection<Categoria> Categorias { get; set; }
        public ICollection<Transaccion> Transacciones { get; set; }
        public ICollection<TipoCuenta> TiposCuentas { get; set; }
    }
}
