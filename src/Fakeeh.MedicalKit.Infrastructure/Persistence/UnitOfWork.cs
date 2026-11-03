using Fakeeh.MedicalKit.Application.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Fakeeh.MedicalKit.Infrastructure.Persistence;

internal sealed class UnitOfWork(IntegrationDbContext db) : IUnitOfWork
{
    private static readonly int[] DuplicateKeyErrors = [2601, 2627];

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && DuplicateKeyErrors.Contains(sql.Number))
        {
            db.ChangeTracker.Clear();
            throw new DuplicateKeyException("A record with the same unique key already exists.", ex);
        }
        catch
        {
            db.ChangeTracker.Clear();
            throw;
        }
    }
}
