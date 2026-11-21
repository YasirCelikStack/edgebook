using System.ComponentModel;
using DevExpress.XtraTab;
using TradingJournal.UI.Forms.Lookups;

namespace TradingJournal.UI.Pages;

// Hesap, enstrüman, strateji ve etiket listeleri sekmeler halinde.
// Var olan liste formları pencere olarak değil, sekmenin içine gömülü kontrol olarak açılır.
[DesignerCategory("Code")]
public class DefinitionsPage : PageBase
{
    public DefinitionsPage(AppState state) : base(state)
    {
        var tabs = new XtraTabControl { Dock = DockStyle.Fill };
        AddTab(tabs, "Hesaplar", new AccountListForm());
        AddTab(tabs, "Enstrümanlar", new InstrumentListForm());
        AddTab(tabs, "Stratejiler", new StrategyListForm());
        AddTab(tabs, "Etiketler", new TagListForm());
        Controls.Add(tabs);
    }

    public override string Title => "Tanımlar";
    public override string Subtitle => "Hesaplar, risk kuralları, enstrümanlar, stratejiler ve etiketler";

    // Liste formları kendi verilerini açılırken yükler; burada yapacak iş yok.
    public override Task RefreshAsync() => Task.CompletedTask;

    private static void AddTab(XtraTabControl tabs, string title, Form form)
    {
        var page = new XtraTabPage { Text = title };
        form.TopLevel = false;
        form.FormBorderStyle = FormBorderStyle.None;
        form.Dock = DockStyle.Fill;
        page.Controls.Add(form);
        tabs.TabPages.Add(page);
        form.Show();
    }
}
