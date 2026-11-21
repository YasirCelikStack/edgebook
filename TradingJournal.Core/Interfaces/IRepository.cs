using System.Linq.Expressions;
using TradingJournal.Core.Entities;

namespace TradingJournal.Core.Interfaces;

// Generic repository arayüzü: her entity için aynı CRUD işlemlerini tek bir sözleşmeyle tanımlar.
// "where T : BaseEntity" kısıtı sayesinde sadece Id'si olan sınıflarla kullanılabilir.
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id);
    Task<List<T>> GetAllAsync();
    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(int id);
}
