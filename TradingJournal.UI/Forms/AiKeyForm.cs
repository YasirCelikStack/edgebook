using System.ComponentModel;
using System.Diagnostics;
using DevExpress.XtraEditors;
using TradingJournal.Core.Exceptions;
using TradingJournal.UI.Forms.Base;
using TradingJournal.UI.Helpers;
using TradingJournal.UI.Theming;

namespace TradingJournal.UI.Forms;

// Kullanıcının kendi Gemini API anahtarını girdiği pencere.
[DesignerCategory("Code")]
public class AiKeyForm : DialogFormBase
{
    private const string AiStudioUrl = "https://aistudio.google.com/apikey";

    private readonly TextEdit _key = new();
    private readonly CheckEdit _delete = new() { Text = "Kayıtlı anahtarımı sil" };

    public AiKeyForm() : base("Gemini API anahtarı", width: 480)
    {
        _key.Properties.UseSystemPasswordChar = true;
        _key.Properties.NullValuePrompt = AiKeyStore.HasUserKey ? "(kayıtlı anahtar var; değiştirmek için yeni anahtarı yapıştır)" : "AIza...";

        var info = new LabelControl
        {
            Text = "Yapay zeka koçu Google Gemini kullanır. Ücretsiz anahtarı Google hesabınla AI Studio'dan alabilirsin " +
                   "(kredi kartı gerekmez). Anahtar bu bilgisayarda şifrelenerek saklanır, kimseyle paylaşılmaz.",
            AutoSizeMode = LabelAutoSizeMode.Vertical
        };
        var link = new LinkLabel { Text = "aistudio.google.com/apikey adresini aç", AutoSize = true, LinkColor = Theme.Accent };
        link.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo(AiStudioUrl) { UseShellExecute = true });

        AddField(" ", info).TextVisible = false;
        AddField(" ", link).TextVisible = false;
        AddField("API anahtarı", _key);
        if (AiKeyStore.HasUserKey)
            AddField(" ", _delete).TextVisible = false;
        FitHeight(AiKeyStore.HasUserKey ? 210 : 180);
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
            throw new DomainException("Bu bir Gemini API anahtarına benzemiyor. AI Studio'dan kopyaladığın anahtarı tam olarak yapıştır.");

        AiKeyStore.SaveUserKey(key);
        return Task.CompletedTask;
    }
}
