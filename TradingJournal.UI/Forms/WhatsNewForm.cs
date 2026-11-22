using System.ComponentModel;
using DevExpress.XtraEditors;
using TradingJournal.UI.Controls;
using TradingJournal.UI.Helpers;
using TradingJournal.UI.Theming;

namespace TradingJournal.UI.Forms;

// "Yenilikler" penceresi: güncellemeden sonraki ilk açılışta, o sürümde gelen özellikleri gösterir.
[DesignerCategory("Code")]
public class WhatsNewForm : XtraForm
{
    public WhatsNewForm(IReadOnlyList<ReleaseNotes.Release> releases, string? headline = null)
    {
        ReleaseNotes.Release newest = releases[0];
        Text = "Yenilikler";
        StartPosition = FormStartPosition.CenterParent;
        AppIcon.Apply(this);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Theme.Background;
        ClientSize = new Size(560, 520);

        var list = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            BackColor = Theme.Background,
            Padding = new Padding(24, 20, 24, 8)
        };
        list.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
        list.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        void FullRow(Control control)
        {
            list.Controls.Add(control);
            list.SetColumnSpan(control, 2);
        }

        FullRow(new Label
        {
            Text = headline ?? $"EdgeBook {newest.Version.ToString(3)} yayında",
            AutoSize = true, Font = Theme.Title, ForeColor = Theme.Text, Margin = new Padding(0, 0, 0, 2)
        });
        FullRow(new Label
        {
            Text = "Bu sürümde neler var:",
            AutoSize = true, Font = Theme.Body, ForeColor = Theme.TextMuted, Margin = new Padding(0, 0, 0, 14)
        });

        const int textWidth = 440;
        foreach (ReleaseNotes.Release release in releases)
        {
            // Birden fazla sürüm atlandıysa (ör. 1.0.1 → 1.0.3) her sürümün maddeleri kendi başlığıyla.
            if (releases.Count > 1)
            {
                FullRow(new Label
                {
                    Text = $"v{release.Version.ToString(3)}",
                    AutoSize = true, Font = Theme.SmallBold, ForeColor = Theme.TextFaint, Margin = new Padding(0, 6, 0, 6)
                });
            }

            foreach (ReleaseNotes.Item item in release.Items)
            {
                list.Controls.Add(new Label
                {
                    Text = item.Glyph,
                    Font = Theme.IconsLarge,
                    ForeColor = Theme.Accent,
                    Size = new Size(40, 40),
                    TextAlign = ContentAlignment.TopLeft,
                    Margin = new Padding(0, 2, 0, 0)
                });

                var text = new FlowLayoutPanel
                {
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents = false,
                    AutoSize = true,
                    BackColor = Theme.Background,
                    Margin = new Padding(0, 0, 0, 14)
                };
                text.Controls.Add(new Label
                {
                    Text = item.Title, AutoSize = true, Font = Theme.Heading, ForeColor = Theme.Text, Margin = new Padding(0, 0, 0, 2)
                });
                text.Controls.Add(new Label
                {
                    Text = item.Description, AutoSize = true, MaximumSize = new Size(textWidth, 0),
                    Font = Theme.Body, ForeColor = Theme.TextMuted, Margin = Padding.Empty
                });
                list.Controls.Add(text);
            }
        }

        var scroller = new XtraScrollableControl { Dock = DockStyle.Fill, BackColor = Theme.Background };
        scroller.Appearance.BackColor = Theme.Background;
        scroller.Controls.Add(list);

        var ok = new FlatButton("Harika, devam", Theme.Glyph.Check, primary: true);
        ok.Click += (_, _) => Close();
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 60, FlowDirection = FlowDirection.RightToLeft,
            BackColor = Theme.Background, Padding = new Padding(16, 10, 16, 10)
        };
        buttons.Controls.Add(ok);

        Controls.Add(scroller);
        Controls.Add(buttons);
        AcceptButton = null;
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode is Keys.Escape or Keys.Enter) Close(); };
    }
}
