using System.ComponentModel;
using DevExpress.XtraEditors;
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

    public AccountEditForm(Account entity) : base(entity, entity.Id == 0 ? "Yeni hesap" : "Hesabı düzenle")
    {
        _currency.Properties.MaxLength = 3;
        _currency.Properties.CharacterCasing = CharacterCasing.Upper;

        AddField("Hesap adı", _name);
        AddField("Broker", _broker);
        AddField("Tür", _type);
        AddField("Para birimi", _currency);
        AddField("Başlangıç bakiyesi", _balance);
        FitHeight(190);
    }

    protected override void LoadValues()
    {
        _name.Text = Entity.Name;
        _broker.Text = Entity.Broker;
        _type.EditValue = Entity.Type;
        _currency.Text = Entity.Currency;
        _balance.Value = IsNew ? 100_000m : Entity.InitialBalance;
    }

    protected override void ApplyValues()
    {
        Entity.Name = _name.Text;
        Entity.Broker = string.IsNullOrWhiteSpace(_broker.Text) ? null : _broker.Text.Trim();
        Entity.Type = (AccountType)_type.EditValue;
        Entity.Currency = _currency.Text;
        Entity.InitialBalance = _balance.Value;
    }
}
