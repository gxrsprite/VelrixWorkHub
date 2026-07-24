using System;
using FreeSql;

internal class DddRepository<TEntity> : AggregateRootRepository<TEntity>, IAggregateRootRepository<TEntity>, IBaseRepository<TEntity>, IBaseRepository, IDisposable where TEntity : class
{
	public override ISelect<TEntity> Select => base.SelectDiy;

	public DddRepository(IFreeSql fsql)
		: base(fsql)
	{
	}

	public DddRepository(IFreeSql fsql, UnitOfWorkManager uowManager)
		: base(fsql, uowManager)
	{
	}
}
