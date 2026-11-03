using Fakeeh.MedicalKit.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;

namespace Fakeeh.MedicalKit.Infrastructure.Persistence;

internal sealed class UnitOfWork(IntegrationDbContext db) : IUnitOfWork
{
    // ORA-00001: unique constraint violated.
    private static readonly int[] DuplicateKeyErrors = [1];

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is OracleException sql && DuplicateKeyErrors.Contains(sql.Number))
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
