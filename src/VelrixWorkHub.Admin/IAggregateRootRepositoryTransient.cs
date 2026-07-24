using System;
using FreeSql;

public interface IAggregateRootRepositoryTransient<TEntity> : IAggregateRootRepository<TEntity>, IBaseRepository<TEntity>, IBaseRepository, IDisposable where TEntity : class
{
}
