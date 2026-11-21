using System.ComponentModel;
using TradingJournal.UI.Theming;

namespace TradingJournal.UI.Pages;

// Sol menüdeki her sayfanın atası. Ana pencere sayfanın türünü bilmeden
// Title/Subtitle'ı gösterir ve RefreshAsync()'i çağırır (çok biçimlilik).
[DesignerCategory("Code")]
public abstract class PageBase : UserControl
{
    protected PageBase(AppState state)
    {
        State = state;
        Dock = DockStyle.Fill;
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = Theme.Body;
        DoubleBuffered = true;
    }

    protected AppState State { get; }

    public abstract string Title { get; }
    public abstract string Subtitle { get; }

    // Sayfa her açıldığında ve seçili hesap değiştiğinde çağrılır.
    public abstract Task RefreshAsync();
}
