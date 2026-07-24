using FreeSql.DataAnnotations;

public abstract class Entity<TKey> : IEntity<TKey>
{
    [Column(Position = 1, IsPrimary = true)]
    public virtual TKey Id { get; set; }

    [Column(Position = 2, IsIdentity = true, CanUpdate = false)]
    public long Seq { get; set; }
}
public abstract class Entity : Entity<Guid>
{
}
