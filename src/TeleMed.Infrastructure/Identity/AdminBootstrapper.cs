using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TeleMed.Application.Admin.AdminUsers;
using TeleMed.Infrastructure.Options;

namespace TeleMed.Infrastructure.Identity;

internal sealed class AdminBootstrapper(IServiceScopeFactory scopes, IOptions<AdminAuthOptions> options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.BootstrapSuperAdminEmail))
        {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AdminUserService>()
            .BootstrapSuperAdminAsync(options.Value.BootstrapSuperAdminEmail, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
