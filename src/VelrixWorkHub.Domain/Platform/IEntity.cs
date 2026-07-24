public interface IEntity<TKey>
{
	/// <summary>
	/// 主键Id
	/// </summary>
	TKey Id { get; set; }
}
