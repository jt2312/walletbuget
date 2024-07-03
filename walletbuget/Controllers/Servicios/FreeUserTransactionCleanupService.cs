using walletbuget.Data;
using walletbuget.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace walletbuget.Controllers.Servicios
{
    public class FreeUserTransactionCleanupService : IHostedService, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private Timer _timer;

        public FreeUserTransactionCleanupService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Ejecutar cada día a la medianoche
            var midnight = DateTime.Today.AddDays(1);
            var initialDelay = midnight - DateTime.Now;
            _timer = new Timer(DoWork, null, initialDelay, TimeSpan.FromDays(1));
            return Task.CompletedTask;
        }

        private async void DoWork(object state)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

                var fechaActual = DateTime.UtcNow;
                var fechaMesAnterior = fechaActual.AddMonths(-1);

                var usuariosFree = await dbContext.ApplicationUsers
                    .Where(u => u.Transacciones.Any(t => t.FechaTransaccion.Month == fechaMesAnterior.Month && t.FechaTransaccion.Year == fechaMesAnterior.Year))
                    .ToListAsync();

                foreach (var usuario in usuariosFree)
                {
                    var roles = await userManager.GetRolesAsync(usuario);
                    if (roles.Contains("free"))
                    {
                        var transaccionesMesAnterior = usuario.Transacciones
                            .Where(t => t.FechaTransaccion.Month == fechaMesAnterior.Month && t.FechaTransaccion.Year == fechaMesAnterior.Year)
                            .ToList();

                        dbContext.Transacciones.RemoveRange(transaccionesMesAnterior);
                    }
                }

                await dbContext.SaveChangesAsync();
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
