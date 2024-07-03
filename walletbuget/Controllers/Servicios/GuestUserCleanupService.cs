using walletbuget.Data;
using Microsoft.EntityFrameworkCore;

namespace walletbuget.Controllers.Servicios
{
    public class GuestUserCleanupService : IHostedService, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private Timer _timer;

        public GuestUserCleanupService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromHours(1));
            return Task.CompletedTask;
        }

        private void DoWork(object state)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Seleccionar solo los usuarios invitados que han expirado
                var expiredGuestUsers = dbContext.ApplicationUsers
                                                 .Include(u => u.Categorias)
                                                 .Include(u => u.TiposCuentas)
                                                 .ThenInclude(tc => tc.Cuentas) // Incluir cuentas relacionadas
                                                 .Include(u => u.Transacciones)
                                                 .Where(u => u.ExpirationDate.HasValue 
                                                        && u.ExpirationDate <= DateTime.UtcNow 
                                                        && u.UserName.StartsWith("invitado_"))
                                                 .ToList();

                if (expiredGuestUsers.Any())
                {
                    foreach (var user in expiredGuestUsers)
                    {
                        // Eliminar información relacionada manualmente
                        foreach (var tipoCuenta in user.TiposCuentas)
                        {
                            dbContext.Cuentas.RemoveRange(tipoCuenta.Cuentas);
                        }
                        dbContext.Categorias.RemoveRange(user.Categorias);
                        dbContext.TiposCuentas.RemoveRange(user.TiposCuentas);
                        dbContext.Transacciones.RemoveRange(user.Transacciones);
                    }

                    // Eliminar los usuarios invitados
                    dbContext.ApplicationUsers.RemoveRange(expiredGuestUsers);
                    dbContext.SaveChanges();
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
