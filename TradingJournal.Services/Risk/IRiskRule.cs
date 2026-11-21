using TradingJournal.Core.Entities;

namespace TradingJournal.Services.Risk;

// Her risk kuralının uyması gereken sözleşme. RiskService kuralların hangi sınıf
// olduğunu bilmez; hepsini IRiskRule olarak görür ve aynı Check() çağrısını yapar (çok biçimlilik).
public interface IRiskRule
{
    string Name { get; }
    RiskCheckResult Check(Account account, IReadOnlyCollection<Trade> trades, DateTime asOf);
}

// Bir kuralın kontrol sonucu.
public record RiskCheckResult(
    string RuleName,
    decimal LimitAmount,
    decimal CurrentLoss)
{
    // Limitin ne kadarının kullanıldığı (0-100+). Arayüzde ilerleme çubuğu olarak gösterilebilir.
    public decimal UsagePercent => LimitAmount == 0 ? 0 : Math.Round(100m * CurrentLoss / LimitAmount, 1);

    public bool IsViolated => CurrentLoss >= LimitAmount;

    // %80'i geçince uyarı ver: kural ihlal edilmeden önce trader'ı durdurmak için.
    public bool IsWarning => !IsViolated && UsagePercent >= 80;

    public string Message => IsViolated
        ? $"{RuleName} İHLAL EDİLDİ: {CurrentLoss:N2} / {LimitAmount:N2}"
        : $"{RuleName}: limitin %{UsagePercent} kullanıldı ({CurrentLoss:N2} / {LimitAmount:N2})";
}
