using System.ComponentModel;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using Microsoft.EntityFrameworkCore;
using DevExpress.XtraLayout;
using DevExpress.XtraLayout.Utils;
using TradingJournal.Core.Entities;
using TradingJournal.Core.Enums;
using TradingJournal.Core.Exceptions;
using TradingJournal.Data;
using TradingJournal.Data.Repositories;
using TradingJournal.UI.Forms.Base;
using TradingJournal.UI.Helpers;

namespace TradingJournal.UI.Forms.Trades;

// Yeni işlem ekleme ve var olan işlemi düzenleme penceresi.
// Seçilen enstrümanın türüne göre ForexTrade (lot) veya CryptoTrade (miktar + kaldıraç) oluşturulur.
[DesignerCategory("Code")]
public class TradeEditForm : DialogFormBase
{
    private readonly int? _tradeId;

    // Pencere açık kaldığı sürece aynı DbContext kullanılır: yüklenen işlem ve etiketler
    // takip edilir, Kaydet'e basınca EF sadece değişenleri yazar.
    private readonly TradingJournalDbContext _db = DbContextFactory.Create();
    private Trade? _trade;
    private List<Instrument> _instruments = new();
    private List<Tag> _allTags = new();
    private List<Strategy> _strategies = new();

    private readonly LookUpEdit _account = EditorFactory.Lookup(nameof(Account.Name));
    private readonly LookUpEdit _instrument = EditorFactory.Lookup(nameof(Instrument.Symbol));
    private readonly LookUpEdit _strategy = EditorFactory.Lookup(nameof(Strategy.Name), allowNull: true);
    private readonly ImageComboBoxEdit _direction = EditorFactory.Enum<TradeDirection>();
    private readonly DateEdit _openTime = EditorFactory.DateTime();
    private readonly SpinEdit _entry = EditorFactory.Decimal();
    private readonly SpinEdit _stop = EditorFactory.Decimal();
    private readonly SpinEdit _takeProfit = EditorFactory.Decimal();
    private readonly SpinEdit _lotSize = EditorFactory.Decimal(decimals: 2);
    private readonly SpinEdit _quantity = EditorFactory.Decimal(decimals: 6);
    private readonly SpinEdit _leverage = EditorFactory.Integer(1, 125);
    private readonly SpinEdit _commission = EditorFactory.Decimal(decimals: 2);
    private readonly SpinEdit _exitPrice = EditorFactory.Decimal();
    private readonly DateEdit _closeTime = EditorFactory.DateTime();
    private readonly RatingControl _rating = new();
    private readonly CheckedComboBoxEdit _tags = new();
    private readonly MemoEdit _notes = new();
    private readonly CheckedListBoxControl _checklist = new() { CheckOnClick = true };

    private readonly LayoutControlItem _lotItem;
    private readonly LayoutControlItem _quantityItem;
    private readonly LayoutControlItem _leverageItem;
    private readonly LayoutControlItem _exitPriceItem;
    private readonly LayoutControlItem _closeTimeItem;
    private readonly LayoutControlItem _checklistItem;

    private const int BaseHeight = 560;
    private const int ChecklistHeight = 150;

    // Kaydedilen işlemin Id'si: yeni işlem eklenince listede onu seçmek için.
    public int? SavedTradeId => _trade?.Id;

    public TradeEditForm(int? tradeId = null)
        : base(tradeId is null ? "Yeni işlem" : "İşlemi düzenle", width: 520)
    {
        _tradeId = tradeId;

        _rating.Properties.ItemCount = 5;
        _tags.Properties.SeparatorChar = ',';
        _tags.Properties.NullText = "(etiket yok)";
        _takeProfit.Properties.NullText = "(yok)";

        AddField("Hesap", _account);
        AddField("Enstrüman", _instrument);
        AddField("Yön", _direction);
        AddField("Açılış zamanı", _openTime);
        AddField("Giriş fiyatı", _entry);
        AddField("Stop loss", _stop);
        AddField("Take profit (0 = yok)", _takeProfit);
        _lotItem = AddField("Lot", _lotSize);
        _quantityItem = AddField("Miktar", _quantity);
        _leverageItem = AddField("Kaldıraç", _leverage);
        AddField("Komisyon", _commission);
        _exitPriceItem = AddField("Çıkış fiyatı", _exitPrice);
        _closeTimeItem = AddField("Kapanış zamanı", _closeTime);
        AddField("Strateji", _strategy);
        _checklistItem = AddField("Kontrol listesi\n(uyduğunu işaretle)", _checklist, height: ChecklistHeight);
        _checklistItem.Visibility = LayoutVisibility.Never;
        AddField("Etiketler", _tags);
        AddField("Uygulama puanı", _rating);
        AddField("Notlar", _notes, height: 90);
        FitHeight(BaseHeight);

        _instrument.EditValueChanged += (_, _) => UpdateTypeSpecificFields();
        _strategy.EditValueChanged += (_, _) => RebuildChecklist();
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        try
        {
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ErrorHelper.Show(this, ex);
            Close();
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _db.Dispose();
        base.OnFormClosed(e);
    }

    private async Task LoadDataAsync()
    {
        _account.Properties.DataSource = await new Repository<Account>(_db).GetAllAsync();
        _instruments = await new Repository<Instrument>(_db).GetAllAsync();
        // Stratejiler kontrol listeleriyle (kurallarıyla) birlikte yüklenir.
        _strategies = await _db.Strategies.Include(s => s.Rules).Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
        _allTags = await new Repository<Tag>(_db).GetAllAsync();

        if (_tradeId is int id)
        {
            _trade = await new TradeRepository(_db).GetWithDetailsAsync(id)
                     ?? throw new DomainException("İşlem bulunamadı, silinmiş olabilir.");

            // Var olan işlemin türü değişemez: Forex işlem kripto enstrümana taşınamaz.
            InstrumentType type = _trade.Instrument.Type;
            _instruments = _instruments.Where(i => i.Type == type).ToList();

            // Pasif bir strateji seçiliyse listede yine de görünsün.
            if (_trade.Strategy is { IsActive: false } inactive)
            {
                await _db.Entry(inactive).Collection(s => s.Rules).LoadAsync();
                _strategies.Add(inactive);
            }
        }

        _strategy.Properties.DataSource = _strategies;

        _instrument.Properties.DataSource = _instruments;
        foreach (Tag tag in _allTags)
            _tags.Properties.Items.Add(tag.Id, tag.Name, CheckState.Unchecked, true);

        if (_trade is null)
            LoadDefaults();
        else
            LoadTrade(_trade);

        UpdateTypeSpecificFields();
        RebuildChecklist();
    }

    private void LoadDefaults()
    {
        var accounts = (List<Account>)_account.Properties.DataSource;
        if (accounts.Count == 0)
            throw new DomainException("Önce Tanımlar > Hesaplar bölümünden bir hesap ekleyin.");

        _account.EditValue = accounts[0].Id;
        _direction.EditValue = TradeDirection.Long;
        _openTime.EditValue = DateTime.Now;
        _lotSize.Value = 1m;
        _quantity.Value = 1m;
        _leverage.Value = 1;

        _exitPriceItem.Visibility = LayoutVisibility.Never;
        _closeTimeItem.Visibility = LayoutVisibility.Never;
    }

    private void LoadTrade(Trade trade)
    {
        _account.EditValue = trade.AccountId;
        _instrument.EditValue = trade.InstrumentId;
        _strategy.EditValue = trade.StrategyId;
        _direction.EditValue = trade.Direction;
        _openTime.EditValue = trade.OpenTime;
        _entry.Value = trade.EntryPrice;
        _stop.Value = trade.StopLoss;
        _takeProfit.Value = trade.TakeProfit ?? 0;
        _commission.Value = trade.Commission;
        _rating.EditValue = (decimal)(trade.Rating ?? 0);
        _notes.Text = trade.Notes;

        switch (trade)
        {
            case ForexTrade forex:
                _lotSize.Value = forex.LotSize;
                break;
            case CryptoTrade crypto:
                _quantity.Value = crypto.Quantity;
                _leverage.Value = crypto.Leverage;
                break;
        }

        // Kapanış alanları sadece kapanmış işlemde düzenlenebilir.
        // Açık işlemi kapatmak için ana penceredeki "İşlemi kapat" kullanılır.
        bool closed = trade.IsClosed;
        _exitPriceItem.Visibility = closed ? LayoutVisibility.Always : LayoutVisibility.Never;
        _closeTimeItem.Visibility = closed ? LayoutVisibility.Always : LayoutVisibility.Never;
        if (closed)
        {
            _exitPrice.Value = trade.ExitPrice!.Value;
            _closeTime.EditValue = trade.CloseTime;
        }

        var tagIds = trade.Tags.Select(t => t.Id).ToHashSet();
        foreach (DevExpress.XtraEditors.Controls.CheckedListBoxItem item in _tags.Properties.Items)
            item.CheckState = tagIds.Contains((int)item.Value) ? CheckState.Checked : CheckState.Unchecked;
        _tags.RefreshEditValue();
    }

    private Instrument? SelectedInstrument
        => _instrument.EditValue is int id ? _instruments.FirstOrDefault(i => i.Id == id) : null;

    private Strategy? SelectedStrategy
        => _strategy.EditValue is int id ? _strategies.FirstOrDefault(s => s.Id == id) : null;

    // Seçili stratejinin kuralları kontrol listesi olarak gösterilir. Düzenlenen işlemde
    // daha önce verilen cevaplar işaretli gelir. Kuralı olmayan stratejide liste gizlenir.
    private void RebuildChecklist()
    {
        _checklist.Items.Clear();
        List<StrategyRule> rules = SelectedStrategy?.Rules.OrderBy(r => r.SortOrder).ToList() ?? new List<StrategyRule>();
        Dictionary<int, bool> answers = _trade?.RuleChecks.ToDictionary(c => c.StrategyRuleId, c => c.IsMet) ?? new Dictionary<int, bool>();

        foreach (StrategyRule rule in rules)
        {
            bool met = answers.TryGetValue(rule.Id, out bool value) && value;
            _checklist.Items.Add(rule.Id, rule.Text, met ? CheckState.Checked : CheckState.Unchecked, true);
        }

        bool visible = rules.Count > 0;
        if ((_checklistItem.Visibility == LayoutVisibility.Always) == visible) return;
        _checklistItem.Visibility = visible ? LayoutVisibility.Always : LayoutVisibility.Never;
        FitHeight(visible ? BaseHeight + ChecklistHeight : BaseHeight);
    }

    // Kontrol listesi cevaplarını işleme yazar. Hiçbir madde işaretlenmediyse liste "doldurulmadı"
    // sayılır ve cevap kaydedilmez (yoksa doldurmayı unutmak "tüm kuralları çiğnedim" gibi görünürdü).
    private void ApplyChecklist(Trade trade)
    {
        Dictionary<int, bool> answers = _checklist.Items
            .Cast<CheckedListBoxItem>()
            .ToDictionary(i => (int)i.Value, i => i.CheckState == CheckState.Checked);
        if (!answers.Values.Any(met => met))
            answers.Clear();

        foreach (TradeRuleCheck old in trade.RuleChecks.Where(c => !answers.ContainsKey(c.StrategyRuleId)).ToList())
            trade.RuleChecks.Remove(old);

        foreach ((int ruleId, bool met) in answers)
        {
            TradeRuleCheck? existing = trade.RuleChecks.FirstOrDefault(c => c.StrategyRuleId == ruleId);
            if (existing is not null)
                existing.IsMet = met;
            else
                trade.RuleChecks.Add(new TradeRuleCheck { StrategyRuleId = ruleId, IsMet = met });
        }
    }

    // Forex seçilince lot, kripto seçilince miktar ve kaldıraç alanı görünür.
    private void UpdateTypeSpecificFields()
    {
        bool isCrypto = SelectedInstrument?.Type == InstrumentType.Crypto;
        _lotItem.Visibility = isCrypto ? LayoutVisibility.Never : LayoutVisibility.Always;
        _quantityItem.Visibility = isCrypto ? LayoutVisibility.Always : LayoutVisibility.Never;
        _leverageItem.Visibility = isCrypto ? LayoutVisibility.Always : LayoutVisibility.Never;
    }

    protected override async Task SaveAsync()
    {
        if (_account.EditValue is not int accountId)
            throw new DomainException("Hesap seçmelisiniz.");
        Instrument instrument = SelectedInstrument
            ?? throw new DomainException("Enstrüman seçmelisiniz.");
        if (_openTime.EditValue is not DateTime openTime)
            throw new DomainException("Açılış zamanını girmelisiniz.");

        // Yeni işlemde türü enstrüman belirler. Kaydetme başarısız olursa
        // yarım nesne context'e eklenmemiş olur, bir sonraki denemede yeniden oluşturulur.
        Trade trade = _trade ?? CreateTrade(instrument);

        trade.AccountId = accountId;
        trade.InstrumentId = instrument.Id;
        trade.StrategyId = _strategy.EditValue as int?;
        trade.Direction = (TradeDirection)_direction.EditValue;
        trade.OpenTime = openTime;
        trade.EntryPrice = _entry.Value;
        trade.StopLoss = _stop.Value;
        trade.TakeProfit = _takeProfit.Value > 0 ? _takeProfit.Value : null;
        trade.Commission = _commission.Value;
        int rating = (int)Math.Round(Convert.ToDecimal(_rating.EditValue ?? 0m));
        trade.Rating = rating > 0 ? rating : null;
        trade.Notes = string.IsNullOrWhiteSpace(_notes.Text) ? null : _notes.Text.Trim();

        switch (trade)
        {
            case ForexTrade forex:
                forex.LotSize = _lotSize.Value;
                break;
            case CryptoTrade crypto:
                crypto.Quantity = _quantity.Value;
                crypto.Leverage = (int)_leverage.Value;
                break;
        }

        ValidatePrices(trade);

        if (trade.IsClosed)
        {
            if (_closeTime.EditValue is not DateTime closeTime)
                throw new DomainException("Kapanış zamanını girmelisiniz.");
            if (closeTime < openTime)
                throw new DomainException("Kapanış zamanı açılış zamanından önce olamaz.");
            trade.ExitPrice = _exitPrice.Value;
            trade.CloseTime = closeTime;
        }

        var selectedTagIds = _tags.Properties.Items.GetCheckedValues().Cast<int>().ToHashSet();
        trade.Tags.Clear();
        foreach (Tag tag in _allTags.Where(t => selectedTagIds.Contains(t.Id)))
            trade.Tags.Add(tag);

        ApplyChecklist(trade);

        var repository = new TradeRepository(_db);
        if (_trade is null)
            await repository.AddAsync(trade);
        else
            await repository.UpdateAsync(trade);

        _trade = trade;
    }

    private static Trade CreateTrade(Instrument instrument) => instrument.Type switch
    {
        // Pip ayarları işlem anında enstrümandan kopyalanır (bkz. ForexTrade.PipSize açıklaması).
        InstrumentType.Forex => new ForexTrade
        {
            PipSize = instrument.PipSize,
            PipValuePerLot = instrument.PipValuePerLot
        },
        InstrumentType.Crypto => new CryptoTrade(),
        _ => throw new DomainException("Desteklenmeyen enstrüman türü.")
    };

    // Stop ve hedefin yöne göre doğru tarafta olup olmadığını kontrol eder.
    private static void ValidatePrices(Trade trade)
    {
        bool isLong = trade.Direction == TradeDirection.Long;
        if (isLong ? trade.StopLoss >= trade.EntryPrice : trade.StopLoss <= trade.EntryPrice)
            throw new DomainException(isLong
                ? "Long işlemde stop loss giriş fiyatının altında olmalıdır."
                : "Short işlemde stop loss giriş fiyatının üstünde olmalıdır.");

        if (trade.TakeProfit is decimal tp && (isLong ? tp <= trade.EntryPrice : tp >= trade.EntryPrice))
            throw new DomainException(isLong
                ? "Long işlemde take profit giriş fiyatının üstünde olmalıdır."
                : "Short işlemde take profit giriş fiyatının altında olmalıdır.");
    }
}
