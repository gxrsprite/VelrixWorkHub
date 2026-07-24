namespace AdminBlazor;

/// <summary>
/// 基础仓储
/// </summary>
public class BasicRepository<TEntity> : FreeSql.BaseRepository<TEntity, Guid>
    where TEntity : class
{
    public BasicRepository(IFreeSql fsql) : base(fsql) { }
}

public class BasicRepository<TEntity, TKey> : FreeSql.BaseRepository<TEntity, TKey>
    where TEntity : class
{
    public BasicRepository(IFreeSql fsql) : base(fsql) { }
}
