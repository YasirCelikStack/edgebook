using System.ComponentModel;
using DevExpress.XtraEditors;
using TradingJournal.Core.Entities;
using TradingJournal.Core.Exceptions;
using TradingJournal.Data;
using TradingJournal.Data.Repositories;
using TradingJournal.UI.Forms.Base;

namespace TradingJournal.UI.Forms.Lookups;

[DesignerCategory("Code")]
public class StrategyListForm : EntityListForm<Strategy>
{
    public StrategyListForm() : base("Stratejiler") { }

    protected override void ConfigureColumns()
    {
        AddColumn(nameof(Strategy.Name), "Strateji", width: 200);
        AddColumn(nameof(Strategy.Description), "Açıklama", width: 320);
        AddColumn(nameof(Strategy.RuleCount), "Kontrol listesi", width: 100);
        AddColumn(nameof(Strategy.IsActive), "Aktif", width: 60);
    }

    // Stratejiler kurallarıyla birlikte yüklenir ve kaydedilir.
    protected override Repository<Strategy> CreateRepository(TradingJournalDbContext db) => new StrategyRepository(db);

    protected override EntityEditForm<Strategy> CreateEditor(Strategy entity) => new StrategyEditForm(entity);

    protected override string GetDeleteMessage(Strategy entity)
        => $"\"{entity.Name}\" silinsin mi? Bu stratejiyi kullanan işlemlerin strateji alanı boşaltılır, kontrol listesi cevapları silinir.";
}

[DesignerCategory("Code")]
public class StrategyEditForm : EntityEditForm<Strategy>
{
    private const int MaxRules = 15;

    private readonly TextEdit _name = new();
    private readonly MemoEdit _description = new();
    private readonly MemoEdit _rules = new();
    private readonly CheckEdit _isActive = new() { Text = "Aktif" };

    public StrategyEditForm(Strategy entity) : base(entity, entity.Id == 0 ? "Yeni strateji" : "Stratejiyi düzenle")
    {
        _description.Properties.MaxLength = 1000;
        _rules.Properties.NullValuePrompt = "Her satıra bir kural: HTF bias ile aynı yönde / Killzone içinde / Likidite alındı";

        AddField("Strateji adı", _name);
        AddField("Açıklama", _description, height: 80);
        AddField("Kontrol listesi\n(her satır bir kural)", _rules, height: 150);
        AddField(" ", _isActive);
        FitHeight(360);
    }

    protected override void LoadValues()
    {
        _name.Text = Entity.Name;
        _description.Text = Entity.Description;
        _rules.Text = string.Join(Environment.NewLine, Entity.Rules.OrderBy(r => r.SortOrder).Select(r => r.Text));
        _isActive.Checked = Entity.IsActive;
    }

    protected override void ApplyValues()
    {
        List<string> lines = (_rules.Text ?? "")
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (lines.Count > MaxRules)
            throw new DomainException($"Kontrol listesinde en fazla {MaxRules} kural olabilir.");
        if (lines.FirstOrDefault(l => l.Length > 200) is string tooLong)
            throw new DomainException($"Kural en fazla 200 karakter olabilir: \"{tooLong[..40]}...\"");

        Entity.Name = _name.Text;
        Entity.Description = string.IsNullOrWhiteSpace(_description.Text) ? null : _description.Text.Trim();
        Entity.IsActive = _isActive.Checked;
        SyncRules(lines);
    }

    // Kural listesini satırlarla eşitler. Var olan kurallar mümkün olduğunca korunur ki
    // geçmiş işlemlerdeki "uyuldu / uyulmadı" cevapları kaybolmasın:
    //   1) Metni aynen duran kural aynen kalır (sırası değişmiş olabilir).
    //   2) Kalan satırlar, eşleşmeyen eski kuralların metnini düzeltilmiş hali sayılır (yazım düzeltmesi).
    //   3) Hâlâ fazla satır varsa yeni kural, fazla eski kural varsa silinir.
    private void SyncRules(List<string> lines)
    {
        List<StrategyRule> unmatched = Entity.Rules.OrderBy(r => r.SortOrder).ToList();
        var result = new StrategyRule?[lines.Count];

        for (int i = 0; i < lines.Count; i++)
        {
            StrategyRule? same = unmatched.FirstOrDefault(r => string.Equals(r.Text, lines[i], StringComparison.OrdinalIgnoreCase));
            if (same is null) continue;
            result[i] = same;
            unmatched.Remove(same);
        }

        for (int i = 0; i < lines.Count; i++)
        {
            if (result[i] is not null) continue;
            if (unmatched.Count > 0)
            {
                result[i] = unmatched[0];
                unmatched.RemoveAt(0);
            }
            else
            {
                result[i] = new StrategyRule { StrategyId = Entity.Id };
            }
        }

        Entity.Rules.Clear();
        for (int i = 0; i < lines.Count; i++)
        {
            StrategyRule rule = result[i]!;
            rule.Text = lines[i];
            rule.SortOrder = i;
            Entity.Rules.Add(rule);
        }
    }
}
