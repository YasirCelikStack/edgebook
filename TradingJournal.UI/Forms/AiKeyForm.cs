using System.ComponentModel;
using System.Diagnostics;
using DevExpress.XtraEditors;
using TradingJournal.Core.Exceptions;
using TradingJournal.UI.Controls;
using TradingJournal.UI.Forms.Base;
using TradingJournal.UI.Helpers;
using TradingJournal.UI.Theming;

namespace TradingJournal.UI.Forms;

// Kullanıcının kendi Gemini API anahtarını girdiği pencere.
// Teknik bilgisi olmayan biri de anahtarı alabilsin diye adım adım anlatım ve kısayol düğmeleri içerir.
[DesignerCategory("Code")]
public class AiKeyForm : DialogFormBase
{
    public const string AiStudioUrl = "https://aistudio.google.com/apikey";

    public const string HowToText =
        "Yapay zeka koçu Google'ın Gemini modelini kullanır. Bunun için ücretsiz bir \"API anahtarı\" gerekir " +
        "(yaklaşık 2 dakika, kredi kartı istemez).\n\n" +
        "1.  Aşağıdaki \"AI Studio'yu aç\" düğmesine bas ve Google hesabınla giriş yap.\n" +
        "2.  İlk kez giriyorsan kullanım koşullarını onayla.\n" +
        "3.  \"Create API key\" (API anahtarı oluştur) düğmesine bas. Proje sorarsa önerileni seç.\n" +
        "4.  Oluşan anahtarın yanındaki kopyala simgesine bas. Anahtar \"AIza\" ile başlar.\n" +
        "5.  Buraya dön, \"Panodan yapıştır\"a bas ve Kaydet.\n\n" +
        "Ücretsiz hak günde yaklaşık 20 istektir (her rapor veya soru 1 istek) ve her gün yenilenir. " +
        "Anahtar sadece bu bilgisayarda, şifrelenmiş olarak saklanır; kimseyle paylaşma.";

    private readonly TextEdit _key = new();
    private readonly CheckEdit _delete = new() { Text = "Kayıtlı anahtarımı sil" };

    public AiKeyForm() : base("Gemini API anahtarı", width: 560)
    {
        _key.Properties.UseSystemPasswordChar = true;
        _key.Properties.NullValuePrompt = AiKeyStore.HasUserKey
            ? "(kayıtlı anahtar var; değiştirmek için yenisini yapıştır)"
            : "AIza...";

        var steps = new LabelControl
        {
            Text = HowToText,
            AutoSizeMode = LabelAutoSizeMode.Vertical
        };
        steps.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;

        var openButton = new FlatButton("AI Studio'yu aç", "", primary: true);
        openButton.Click += (_, _) => Process.Start(new ProcessStartInfo(AiStudioUrl) { UseShellExecute = true });

        var pasteButton = new FlatButton("Panodan yapıştır", "");
        pasteButton.Click += (_, _) =>
        {
            string text = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : string.Empty;
            if (text.Length == 0)
            {
                XtraMessageBox.Show(this, "Panoda metin yok. Önce AI Studio'da anahtarın yanındaki kopyala simgesine bas.",
                    "Gemini anahtarı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _key.Text = text;
        };

        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Color.Transparent };
        buttons.Controls.Add(openButton);
        buttons.Controls.Add(pasteButton);

        AddField(" ", steps, height: 250).TextVisible = false;
        AddField(" ", buttons, height: 46).TextVisible = false;
        AddField("API anahtarı", _key);
        if (AiKeyStore.HasUserKey)
            AddField(" ", _delete).TextVisible = false;
        FitHeight(AiKeyStore.HasUserKey ? 390 : 360);
    }

    protected override Task SaveAsync()
    {
        if (_delete.Checked)
        {
            AiKeyStore.DeleteUserKey();
            return Task.CompletedTask;
        }

        string key = _key.Text.Trim();
        if (key.Length == 0)
            throw new DomainException("Anahtarı yapıştır ya da İptal'e bas.");
        if (key.Length < 20 || key.Contains(' '))
            throw new DomainException("Bu bir Gemini API anahtarına benzemiyor. AI Studio'da anahtarın yanındaki kopyala simgesine basıp tekrar yapıştır.");
        if (!key.StartsWith("AIza", StringComparison.Ordinal)
            && XtraMessageBox.Show(this, "Gemini anahtarları genelde \"AIza\" ile başlar. Yine de kaydedilsin mi?",
                "Gemini anahtarı", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            throw new DomainException("Anahtarı AI Studio'dan tekrar kopyala.");

        AiKeyStore.SaveUserKey(key);
        return Task.CompletedTask;
    }
}
