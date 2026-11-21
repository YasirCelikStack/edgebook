using System.ComponentModel;
using DevExpress.XtraEditors;
using TradingJournal.Core.Entities;
using TradingJournal.Core.Enums;
using TradingJournal.UI.Forms.Base;
using TradingJournal.UI.Helpers;

namespace TradingJournal.UI.Forms.Lookups;

[DesignerCategory("Code")]
public class InstrumentListForm : EntityListForm<Instrument>
{
    public InstrumentListForm() : base("Enstrümanlar") { }

    protected override void ConfigureColumns()
    {
        AddColumn(nameof(Instrument.Symbol), "Sembol", width: 120);
        AddColumn(nameof(Instrument.Type), "Tür");
        AddColumn(nameof(Instrument.PipSize), "Pip büyüklüğü", "0.#####");
        AddColumn(nameof(Instrument.PipValuePerLot), "1 lotta pip değeri", "n2");
    }

    protected override EntityEditForm<Instrument> CreateEditor(Instrument entity) => new InstrumentEditForm(entity);
}

[DesignerCategory("Code")]
public class InstrumentEditForm : EntityEditForm<Instrument>
{
    private readonly TextEdit _symbol = new();
    private readonly ImageComboBoxEdit _type = EditorFactory.Enum<InstrumentType>();
    private readonly SpinEdit _pipSize = EditorFactory.Decimal(decimals: 5);
    private readonly SpinEdit _pipValue = EditorFactory.Decimal(decimals: 2);

    public InstrumentEditForm(Instrument entity) : base(entity, entity.Id == 0 ? "Yeni enstrüman" : "Enstrümanı düzenle")
    {
        _symbol.Properties.CharacterCasing = CharacterCasing.Upper;

        AddField("Sembol", _symbol);
        AddField("Tür", _type);
        AddField("Pip büyüklüğü", _pipSize);
        AddField("1 lotta pip değeri ($)", _pipValue);
        FitHeight(160);
    }

    protected override void LoadValues()
    {
        _symbol.Text = Entity.Symbol;
        _type.EditValue = Entity.Type;
        _pipSize.Value = Entity.PipSize;
        _pipValue.Value = Entity.PipValuePerLot;
    }

    protected override void ApplyValues()
    {
        Entity.Symbol = _symbol.Text;
        Entity.Type = (InstrumentType)_type.EditValue;
        Entity.PipSize = _pipSize.Value;
        Entity.PipValuePerLot = _pipValue.Value;
    }
}
