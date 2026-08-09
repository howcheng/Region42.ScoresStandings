using System.Linq.Expressions;
using Region42.ScoresStandings.Domain.Entities;

namespace Region42.ScoresStandings.Domain.Interfaces;

public interface IRepository<T> where T : BaseEntity
{
	Task<T?> GetByIdAsync(int id);
	Task<IEnumerable<T>> GetAllAsync();
	Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
	Task AddAsync(T entity);
	void Update(T entity);
	void Delete(T entity);
	Task<int> SaveChangesAsync();
}
