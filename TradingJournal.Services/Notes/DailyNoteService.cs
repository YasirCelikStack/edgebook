using Microsoft.EntityFrameworkCore;
using TradingJournal.Core.Entities;
using TradingJournal.Data;

namespace TradingJournal.Services.Notes;

// Günlük notları (seans öncesi plan + gün sonu değerlendirmesi) okur ve kaydeder.
public class DailyNoteService
{
    public async Task<DailyNote?> GetAsync(DateTime date)
    {
        using var db = DbContextFactory.Create();
        DateTime day = date.Date;
        return await db.DailyNotes.AsNoTracking().FirstOrDefaultAsync(n => n.Date == day);
    }

    // [from, to) aralığındaki notlar (takvimde işaret, rapor ve yapay zeka bağlamı için).
    public async Task<List<DailyNote>> GetRangeAsync(DateTime from, DateTime to)
    {
        using var db = DbContextFactory.Create();
        DateTime start = from.Date, end = to.Date;
        return await db.DailyNotes.AsNoTracking()
            .Where(n => n.Date >= start && n.Date < end)
            .OrderBy(n => n.Date)
            .ToListAsync();
    }

    // Kaydeder; not tamamen boşaltıldıysa siler. Aynı güne ikinci kayıt açılmaz (Date benzersiz).
    public async Task SaveAsync(DateTime date, string? plan, string? review, int? mood)
    {
        using var db = DbContextFactory.Create();
        DateTime day = date.Date;
        DailyNote? note = await db.DailyNotes.FirstOrDefaultAsync(n => n.Date == day);
        note ??= db.DailyNotes.Add(new DailyNote { Date = day }).Entity;

        note.Plan = Clean(plan);
        note.Review = Clean(review);
        note.Mood = mood is >= 1 and <= 5 ? mood : null;
        note.UpdatedAt = DateTime.Now;

        if (note.IsEmpty)
        {
            if (note.Id == 0)
                return; // Hiç kaydedilmemiş boş not: yapılacak bir şey yok.
            db.DailyNotes.Remove(note);
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new DataAccessException("Günlük not kaydedilemedi.", ex);
        }
    }

    private static string? Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        string trimmed = text.Trim();
        return trimmed.Length > 4000 ? trimmed[..4000] : trimmed;
    }
}
