using System.ComponentModel;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Views.Base;
using TradingJournal.Core.Entities;
using TradingJournal.UI.Forms.Base;

namespace TradingJournal.UI.Forms.Lookups;

[DesignerCategory("Code")]
public class TagListForm : EntityListForm<Tag>
{
    public TagListForm() : base("Etiketler") { }

    protected override void ConfigureColumns()
    {
        AddColumn(nameof(Tag.Name), "Etiket", width: 200);
        AddColumn(nameof(Tag.ColorHex), "Renk", width: 90);

        // Renk sütununun hücresini etiketin kendi rengiyle boya.
        View.CustomDrawCell += (_, e) =>
        {
            if (e.Column.FieldName != nameof(Tag.ColorHex) || e.CellValue is not string hex)
                return;
            if (TryParseColor(hex, out Color color))
                e.Appearance.BackColor = color;
        };
    }

    protected override EntityEditForm<Tag> CreateEditor(Tag entity) => new TagEditForm(entity);

    internal static bool TryParseColor(string? hex, out Color color)
    {
        color = Color.Empty;
        if (string.IsNullOrWhiteSpace(hex))
            return false;
        try
        {
            color = ColorTranslator.FromHtml(hex);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
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
        _color.Color = TagListForm.TryParseColor(Entity.ColorHex, out Color color) ? color : Color.SteelBlue;
    }

    protected override void ApplyValues()
    {
        Entity.Name = _name.Text;
        Color c = _color.Color;
        Entity.ColorHex = c.IsEmpty ? null : $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }
}
