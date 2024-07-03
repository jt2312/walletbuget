using walletbuget.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace walletbuget.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions options) : base(options)
        {

        }

        public DbSet<ApplicationUser> ApplicationUsers { get; set; }
        public DbSet<Categoria> Categorias { get; set; }
        public DbSet<TipoOperacion> TipoOperaciones { get; set; }
        public DbSet<TipoCuenta> TiposCuentas { get; set; }
        public DbSet<Cuenta> Cuentas { get; set; }
        public DbSet<Transaccion> Transacciones { get; set; }
        public DbSet<MesCerrado> MesesCerrados { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TipoOperacion>().HasData(new TipoOperacion
            {
                Id = 1,
                Descripcion = "Ingresos"
            }, new TipoOperacion
            {
                Id = 2,
                Descripcion = "Gastos"
            });

        }
    }
}
