using System.ComponentModel;
using DevExpress.XtraEditors;
using Microsoft.EntityFrameworkCore;
using TradingJournal.Data;
using TradingJournal.Core.Entities;
using TradingJournal.Core.Enums;
using TradingJournal.UI.Forms.Base;
using TradingJournal.UI.Helpers;

namespace TradingJournal.UI.Forms.Lookups;

[DesignerCategory("Code")]
public class AccountListForm : EntityListForm<Account>
{
    public AccountListForm() : base("Hesaplar") { }

    protected override void ConfigureColumns()
    {
        AddColumn(nameof(Account.Name), "Hesap adı", width: 180);
        AddColumn(nameof(Account.Broker), "Broker");
        AddColumn(nameof(Account.Type), "Tür");
        AddColumn(nameof(Account.Currency), "Para birimi", width: 70);
        AddColumn(nameof(Account.InitialBalance), "Başlangıç bakiyesi", "n2");
    }

    protected override EntityEditForm<Account> CreateEditor(Account entity) => new AccountEditForm(entity);

    // Hesapları risk kurallarıyla birlikte yükle: düzenleme formu kuralları da gösterir.
    protected override Task<List<Account>> LoadItemsAsync(TradingJournalDbContext db)
        => db.Accounts.Include(a => a.Rules).OrderBy(a => a.Name).ToListAsync();

    // Hesap silinince ona bağlı tüm işlemler de silinir (cascade), bu yüzden açıkça uyarıyoruz.
    protected override string GetDeleteMessage(Account entity)
        => $"\"{entity.Name}\" hesabı ve bu hesaba ait TÜM işlemler silinecek. Emin misiniz?";
}

[DesignerCategory("Code")]
public class AccountEditForm : EntityEditForm<Account>
{
    private readonly TextEdit _name = new();
    private readonly TextEdit _broker = new();
    private readonly ImageComboBoxEdit _type = EditorFactory.Enum<AccountType>();
    private readonly TextEdit _currency = new();
    private readonly SpinEdit _balance = EditorFactory.Decimal(decimals: 2);
    private readonly SpinEdit _dailyLoss = EditorFactory.Decimal(decimals: 2, max: 100);
    private readonly SpinEdit _maxDrawdown = EditorFactory.Decimal(decimals: 2, max: 100);

    public AccountEditForm(Account entity) : base(entity, entity.Id == 0 ? "Yeni hesap" : "Hesabı düzenle")
    {
        _currency.Properties.MaxLength = 3;
        _currency.Properties.CharacterCasing = CharacterCasing.Upper;

        AddField("Hesap adı", _name);
        AddField("Broker", _broker);
        AddField("Tür", _type);
        AddField("Para birimi", _currency);
        AddField("Başlangıç bakiyesi", _balance);
        AddField("Günlük kayıp limiti % (0 = yok)", _dailyLoss);
        AddField("Max drawdown % (0 = yok)", _maxDrawdown);
        FitHeight(250);
    }

    protected override void LoadValues()
    {
        _name.Text = Entity.Name;
        _broker.Text = Entity.Broker;
        _type.EditValue = Entity.Type;
        _currency.Text = Entity.Currency;
        _balance.Value = IsNew ? 100_000m : Entity.InitialBalance;
        _dailyLoss.Value = ActiveLimit(RiskRuleType.DailyLoss);
        _maxDrawdown.Value = ActiveLimit(RiskRuleType.MaxDrawdown);
    }

    protected override void ApplyValues()
    {
        Entity.Name = _name.Text;
        Entity.Broker = string.IsNullOrWhiteSpace(_broker.Text) ? null : _broker.Text.Trim();
        Entity.Type = (AccountType)_type.EditValue;
        Entity.Currency = _currency.Text;
        Entity.InitialBalance = _balance.Value;
        SetRule(RiskRuleType.DailyLoss, _dailyLoss.Value);
        SetRule(RiskRuleType.MaxDrawdown, _maxDrawdown.Value);
    }

    private decimal ActiveLimit(RiskRuleType type)
        => Entity.Rules.FirstOrDefault(r => r.RuleType == type && r.IsActive)?.LimitPercent ?? 0;

    // Limit 0 ise kural pasifleştirilir (silinmez), değilse güncellenir veya yeni eklenir.
    private void SetRule(RiskRuleType type, decimal percent)
    {
        AccountRule? rule = Entity.Rules.FirstOrDefault(r => r.RuleType == type);
        if (percent <= 0)
        {
            if (rule is not null) rule.IsActive = false;
            return;
        }

        if (rule is null)
        {
            rule = new AccountRule { RuleType = type };
            Entity.Rules.Add(rule);
        }
        rule.LimitPercent = percent;
        rule.IsActive = true;
    }
}
