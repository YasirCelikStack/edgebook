using System.ComponentModel;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using TradingJournal.Core.Entities;
using TradingJournal.Data;
using TradingJournal.Data.Repositories;
using TradingJournal.UI.Helpers;

namespace TradingJournal.UI.Forms.Base;

// Tanım tablolarının (hesap, enstrüman, strateji, etiket) listesini gösteren generic pencere.
// Ekle / Düzenle / Sil işlemleri tüm tablolar için aynı olduğundan burada bir kez yazılır;
// alt sınıf sadece hangi sütunların görüneceğini ve hangi düzenleme penceresinin açılacağını söyler.
// "new()" kısıtı: Ekle'ye basınca boş bir T nesnesi oluşturabilmek için.
[DesignerCategory("Code")]
public abstract class EntityListForm<T> : XtraForm where T : BaseEntity, new()
{
    protected GridControl Grid { get; }
    protected GridView View { get; }

    protected EntityListForm(string title)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        TradingJournal.UI.Helpers.AppIcon.Apply(this);
        ClientSize = new Size(720, 440);
        ShowInTaskbar = false;

        Grid = new GridControl { Dock = DockStyle.Fill };
        View = new GridView(Grid);
        Grid.MainView = View;

        View.OptionsBehavior.Editable = false;
        View.OptionsBehavior.AutoPopulateColumns = false;
        View.OptionsDetail.EnableMasterViewMode = false; // Trades koleksiyonunu alt tablo olarak gösterme.
        View.OptionsView.ShowGroupPanel = false;
        View.OptionsFind.AlwaysVisible = true;
        View.OptionsSelection.EnableAppearanceFocusedCell = false;
        View.DoubleClick += async (_, _) => await EditSelectedAsync();

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6) };
        toolbar.Controls.Add(CreateButton("Ekle", async () => await AddAsync()));
        toolbar.Controls.Add(CreateButton("Düzenle", async () => await EditSelectedAsync()));
        toolbar.Controls.Add(CreateButton("Sil", async () => await DeleteSelectedAsync()));

        Controls.Add(Grid);
        Controls.Add(toolbar);
    }

    // --- Alt sınıfların doldurduğu kısımlar ---

    protected abstract void ConfigureColumns();
    protected abstract EntityEditForm<T> CreateEditor(T entity);

    // Silmeden önce sorulacak soru. Hesap gibi ilişkili kaydı olanlar daha ciddi uyarı verebilir.
    protected virtual string GetDeleteMessage(T entity) => $"\"{entity}\" silinsin mi?";

    // Listeyi yükler. İlişkili verisi de gereken tablolar (hesap + kuralları gibi) bunu ezer.
    protected virtual Task<List<T>> LoadItemsAsync(TradingJournalDbContext db)
        => CreateRepository(db).GetAllAsync();

    // Alt sınıf özel bir repository kullanabilir (ör. stratejiler kurallarıyla birlikte kaydedilir).
    protected virtual Repository<T> CreateRepository(TradingJournalDbContext db) => new(db);

    // --- Ortak davranış ---

    protected GridColumn AddColumn(string fieldName, string caption, string? format = null, int width = 0)
    {
        GridColumn column = View.Columns.AddVisible(fieldName, caption);
        if (format is not null)
        {
            column.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
            column.DisplayFormat.FormatString = format;
        }
        if (width > 0)
            column.Width = width;
        return column;
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ConfigureColumns();
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            // Her işlem kendi kısa ömürlü DbContext'ini kullanır: veriler hep güncel kalır.
            using var db = DbContextFactory.Create();
            Grid.DataSource = await LoadItemsAsync(db);
        }
        catch (Exception ex)
        {
            ErrorHelper.Show(this, ex);
        }
    }

    private async Task AddAsync()
    {
        var entity = new T();
        using EntityEditForm<T> editor = CreateEditor(entity);
        if (editor.ShowDialog(this) != DialogResult.OK)
            return;

        await RunAndReloadAsync(repo => repo.AddAsync(entity));
    }

    private async Task EditSelectedAsync()
    {
        if (View.GetFocusedRow() is not T entity)
            return;

        using EntityEditForm<T> editor = CreateEditor(entity);
        if (editor.ShowDialog(this) != DialogResult.OK)
        {
            // İptal edilse bile nesnede yarım kalmış değişiklik olabilir; listeyi tazele.
            await ReloadAsync();
            return;
        }

        await RunAndReloadAsync(repo => repo.UpdateAsync(entity));
    }

    private async Task DeleteSelectedAsync()
    {
        if (View.GetFocusedRow() is not T entity)
            return;

        if (!ErrorHelper.Confirm(this, GetDeleteMessage(entity)))
            return;

        await RunAndReloadAsync(repo => repo.DeleteAsync(entity.Id));
    }

    private async Task RunAndReloadAsync(Func<Repository<T>, Task> action)
    {
        try
        {
            using var db = DbContextFactory.Create();
            await action(CreateRepository(db));
        }
        catch (Exception ex)
        {
            ErrorHelper.Show(this, ex);
        }
        await ReloadAsync();
    }

    private static SimpleButton CreateButton(string text, Func<Task> onClick)
    {
        var button = new SimpleButton { Text = text, Width = 96 };
        button.Click += async (_, _) => await onClick();
        return button;
    }
}
