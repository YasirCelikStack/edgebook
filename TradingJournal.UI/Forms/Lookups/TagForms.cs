using System.ComponentModel;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Views.Base;
using TradingJournal.Core.Entities;
using TradingJournal.UI.Forms.Base;
using TradingJournal.UI.Helpers;
// Form sınıfının içinde "Tag" yazınca Control.Tag özelliği anlaşılıyor; entity için takma ad kullanıyoruz.
using TagEntity = TradingJournal.Core.Entities.Tag;

namespace TradingJournal.UI.Forms.Lookups;

[DesignerCategory("Code")]
public class TagListForm : EntityListForm<Tag>
{
    public TagListForm() : base("Etiketler") { }

    protected override void ConfigureColumns()
    {
        AddColumn(nameof(TagEntity.Name), "Etiket", width: 200);
        AddColumn(nameof(TagEntity.ColorHex), "Renk", width: 90);

        // Renk sütununun hücresini etiketin kendi rengiyle boya.
        View.CustomDrawCell += (_, e) =>
        {
            if (e.Column.FieldName != nameof(TagEntity.ColorHex) || e.CellValue is not string hex)
                return;
            if (ColorParser.TryParse(hex, out Color color))
                e.Appearance.BackColor = color;
        };
    }

    protected override EntityEditForm<Tag> CreateEditor(Tag entity) => new TagEditForm(entity);
}

[DesignerCategory("Code")]
public class TagEditForm : EntityEditForm<Tag>
{
    private readonly TextEdit _name = new();
    private readonly ColorPickEdit _color = new();

    public TagEditForm(Tag entity) : base(entity, entity.Id == 0 ? "Yeni etiket" : "Etiketi düzenle")
    {
        _name.Properties.MaxLength = 40;
        AddField("Etiket adı", _name);
        AddField("Renk", _color);
        FitHeight(100);
    }

    protected override void LoadValues()
    {
        _name.Text = Entity.Name;
        _color.Color = ColorParser.TryParse(Entity.ColorHex, out Color color) ? color : Color.SteelBlue;
    }

    protected override void ApplyValues()
    {
        Entity.Name = _name.Text;
        Entity.ColorHex = _color.Color.IsEmpty ? null : ColorParser.ToHex(_color.Color);
    }
}
