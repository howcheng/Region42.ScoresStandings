using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Web.Data;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
	private readonly IRegion42DbContext _context;

	public Repository(IRegion42DbContext context)
	{
		_context = context;
	}

	public async Task<T?> GetByIdAsync(int id)
	{
		return await _context.Set<T>().FirstOrDefaultAsync(e => e.Id == id);
	}

	public async Task<IEnumerable<T>> GetAllAsync()
	{
		return await _context.Set<T>().ToListAsync();
	}

	public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
	{
		return await _context.Set<T>().Where(predicate).ToListAsync();
	}

	public async Task AddAsync(T entity)
	{
		_context.Add(entity);
		await Task.CompletedTask;
	}

	public void Update(T entity)
	{
		_context.Update(entity);
	}

	public void Delete(T entity)
	{
		_context.Remove(entity);
	}

	public async Task<int> SaveChangesAsync()
	{
		return await _context.SaveChangesAsync();
	}
}
