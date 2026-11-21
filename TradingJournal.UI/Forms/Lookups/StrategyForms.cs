using System.ComponentModel;
using DevExpress.XtraEditors;
using TradingJournal.Core.Entities;
using TradingJournal.UI.Forms.Base;

namespace TradingJournal.UI.Forms.Lookups;

[DesignerCategory("Code")]
public class StrategyListForm : EntityListForm<Strategy>
{
    public StrategyListForm() : base("Stratejiler") { }

    protected override void ConfigureColumns()
    {
        AddColumn(nameof(Strategy.Name), "Strateji", width: 200);
        AddColumn(nameof(Strategy.Description), "Açıklama", width: 360);
        AddColumn(nameof(Strategy.IsActive), "Aktif", width: 60);
    }

    protected override EntityEditForm<Strategy> CreateEditor(Strategy entity) => new StrategyEditForm(entity);

    protected override string GetDeleteMessage(Strategy entity)
        => $"\"{entity.Name}\" silinsin mi? Bu stratejiyi kullanan işlemlerin strateji alanı boşaltılır.";
}

[DesignerCategory("Code")]
public class StrategyEditForm : EntityEditForm<Strategy>
{
    private readonly TextEdit _name = new();
    private readonly MemoEdit _description = new();
    private readonly CheckEdit _isActive = new() { Text = "Aktif" };

    public StrategyEditForm(Strategy entity) : base(entity, entity.Id == 0 ? "Yeni strateji" : "Stratejiyi düzenle")
    {
        _description.Properties.MaxLength = 1000;

        AddField("Strateji adı", _name);
        AddField("Açıklama", _description, height: 100);
        AddField(" ", _isActive);
        FitHeight(220);
    }

    protected override void LoadValues()
    {
        _name.Text = Entity.Name;
        _description.Text = Entity.Description;
        _isActive.Checked = Entity.IsActive;
    }

    protected override void ApplyValues()
    {
        Entity.Name = _name.Text;
        Entity.Description = string.IsNullOrWhiteSpace(_description.Text) ? null : _description.Text.Trim();
        Entity.IsActive = _isActive.Checked;
    }
}
