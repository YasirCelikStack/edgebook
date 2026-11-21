using Microsoft.EntityFrameworkCore;
using TradingJournal.Core.Entities;
using TradingJournal.Data;
using TradingJournal.Data.Repositories;

namespace TradingJournal.UI;

// Sayfaların ortak kullandığı durum: hangi hesabın seçili olduğu ve verinin nasıl yükleneceği.
// Üst çubuktan hesap değişince AccountChanged olayı ile tüm sayfalar haberdar olur.
public class AppState
{
    public int? AccountId { get; private set; }

    public event EventHandler? AccountChanged;

    public void SelectAccount(int? accountId)
    {
        if (AccountId == accountId) return;
        AccountId = accountId;
        AccountChanged?.Invoke(this, EventArgs.Empty);
    }

    // Seçili hesabın (veya tüm hesapların) işlemleri; hesap, enstrüman, etiket vb. dahil.
    public async Task<List<Trade>> LoadTradesAsync()
    {
        using var db = DbContextFactory.Create();
        var repository = new TradeRepository(db);
        return AccountId is int id
            ? await repository.GetByAccountAsync(id)
            : await repository.GetAllWithDetailsAsync();
    }

    public async Task<List<Account>> LoadAccountsAsync()
    {
        using var db = DbContextFactory.Create();
        return await db.Accounts.Include(a => a.Rules).AsNoTracking().OrderBy(a => a.Name).ToListAsync();
    }

    // Ekran görüntüsü sayıları (liste sütunu için), tek sorguda.
    public async Task<Dictionary<int, int>> LoadScreenshotCountsAsync()
    {
        using var db = DbContextFactory.Create();
        return await db.TradeScreenshots
            .GroupBy(s => s.TradeId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
    }

    // Her işlemin ilk eklenen ekran görüntüsü (listede önizleme olarak gösterilir).
    public async Task<Dictionary<int, TradeScreenshot>> LoadFirstScreenshotsAsync()
    {
        using var db = DbContextFactory.Create();
        List<TradeScreenshot> all = await db.TradeScreenshots.AsNoTracking().ToListAsync();
        return all.GroupBy(s => s.TradeId)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.CreatedAt).First());
    }
}
