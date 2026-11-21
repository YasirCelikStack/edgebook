using Microsoft.EntityFrameworkCore;
using TradingJournal.Core.Entities;
using TradingJournal.Core.Interfaces;

namespace TradingJournal.Data.Repositories;

// Generic repository'den türer (kalıtım), işlemlere özel sorguları ekler.
public class TradeRepository : Repository<Trade>, ITradeRepository
{
    public TradeRepository(TradingJournalDbContext context) : base(context)
    {
    }

    // İlişkili tabloları tek sorguda yükleyen temel sorgu.
    private IQueryable<Trade> WithDetails() => Set
        .Include(t => t.Account)
        .Include(t => t.Instrument)
        .Include(t => t.Strategy)
        .Include(t => t.Tags)
        .Include(t => t.Screenshots);

    public async Task<List<Trade>> GetAllWithDetailsAsync()
        => await WithDetails().OrderByDescending(t => t.OpenTime).ToListAsync();

    public async Task<List<Trade>> GetByAccountAsync(int accountId)
        => await WithDetails()
            .Where(t => t.AccountId == accountId)
            .OrderBy(t => t.OpenTime)
            .ToListAsync();

    public async Task<Trade?> GetWithDetailsAsync(int id)
        => await WithDetails().FirstOrDefaultAsync(t => t.Id == id);
}
