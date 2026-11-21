using TradingJournal.Core.Entities;

namespace TradingJournal.Core.Interfaces;

// İşlemlere özel sorgular: generic arayüzü genişletir (arayüz kalıtımı).
public interface ITradeRepository : IRepository<Trade>
{
    // Hesap, enstrüman, strateji, etiket ve ekran görüntüleriyle birlikte getirir.
    Task<List<Trade>> GetAllWithDetailsAsync();
    Task<List<Trade>> GetByAccountAsync(int accountId);
    Task<Trade?> GetWithDetailsAsync(int id);
}
