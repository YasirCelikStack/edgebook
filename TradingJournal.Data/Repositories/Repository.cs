using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TradingJournal.Core.Entities;
using TradingJournal.Core.Interfaces;

namespace TradingJournal.Data.Repositories;

// IRepository<T> arayüzünün EF Core ile yazılmış genel uygulaması.
// Account, Instrument, Strategy, Tag... hepsi bu tek sınıfla yönetilir: new Repository<Tag>(context)
public class Repository<T> : IRepository<T> where T : BaseEntity
{
    // protected: alt sınıflar (TradeRepository gibi) bu alanları kullanabilsin diye.
    protected readonly TradingJournalDbContext Context;
    protected readonly DbSet<T> Set;

    public Repository(TradingJournalDbContext context)
    {
        Context = context;
        Set = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(int id)
        => await Set.FindAsync(id);

    public virtual async Task<List<T>> GetAllAsync()
        => await Set.ToListAsync();

    public virtual async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate)
        => await Set.Where(predicate).ToListAsync();

    public virtual async Task AddAsync(T entity)
    {
        await Set.AddAsync(entity);
        await SaveAsync("Kayıt eklenemedi.");
    }

    public virtual async Task UpdateAsync(T entity)
    {
        // Nesne zaten takip ediliyorsa EF değişiklikleri kendisi bulur; değilse elle işaretlenir.
        if (Context.Entry(entity).State == EntityState.Detached)
            Set.Update(entity);

        await SaveAsync("Kayıt güncellenemedi.");
    }

    public virtual async Task DeleteAsync(int id)
    {
        T? entity = await Set.FindAsync(id);
        if (entity is null)
            return;

        Set.Remove(entity);
        await SaveAsync("Kayıt silinemedi. Başka kayıtlar tarafından kullanılıyor olabilir.");
    }

    // Tüm yazma işlemleri buradan geçer: veritabanı hatası olursa anlamlı bir mesaja çevrilir.
    protected async Task SaveAsync(string errorMessage)
    {
        try
        {
            await Context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Başarısız kaydı bellekte bırakmayalım; yoksa sonraki her kayıtta yine hata verir.
            Context.ChangeTracker.Clear();
            throw new DataAccessException(errorMessage, ex);
        }
    }
}
