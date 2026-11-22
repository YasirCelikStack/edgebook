using Microsoft.EntityFrameworkCore;
using TradingJournal.Core.Entities;

namespace TradingJournal.Data.Repositories;

// Strateji kaydederken kontrol listesini (kuralları) de eşitler.
// Generic Repository<T>.UpdateAsync, listeden ÇIKARILAN kuralları silmeyi bilmez;
// bu sınıf o davranışı ezer (override): çok biçimlilik örneği.
public class StrategyRepository : Repository<Strategy>
{
    public StrategyRepository(TradingJournalDbContext context) : base(context)
    {
    }

    public override async Task<List<Strategy>> GetAllAsync()
        => await Set.Include(s => s.Rules).OrderBy(s => s.Name).ToListAsync();

    public override async Task UpdateAsync(Strategy entity)
    {
        // Formda silinen kurallar: veritabanında olup nesnede artık olmayanlar.
        // (Bu kurallara ait işlem cevapları da cascade ile silinir.)
        HashSet<int> keep = entity.Rules.Where(r => r.Id != 0).Select(r => r.Id).ToHashSet();
        List<StrategyRule> removed = await Context.StrategyRules
            .Where(r => r.StrategyId == entity.Id && !keep.Contains(r.Id))
            .ToListAsync();
        Context.StrategyRules.RemoveRange(removed);

        await base.UpdateAsync(entity);
    }
}
