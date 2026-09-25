using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TeleMed.Domain.Entities;
using TeleMed.Infrastructure.Persistence;

namespace TeleMed.Infrastructure.Identity;

// Services commit through IUnitOfWork, so Identity must only stage changes on the shared DbContext.
internal sealed class NonSavingUserStore : UserOnlyStore<User, AppDbContext, Guid>
{
    public NonSavingUserStore(AppDbContext context, IdentityErrorDescriber? describer = null)
        : base(context, describer)
    {
        AutoSaveChanges = false;
    }

    // The base Attach()es the user, which resets a tracked entity's original concurrency stamp to the in-memory value
    // and makes a second update in the same unit of work fail. Tracked users are change-detected anyway.
    public override Task<IdentityResult> UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        if (Context.Entry(user).State == EntityState.Detached)
        {
            Context.Update(user);
        }

        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        return Task.FromResult(IdentityResult.Success);
    }
}
