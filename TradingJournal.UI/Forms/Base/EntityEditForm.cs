using System.ComponentModel;
using TradingJournal.Core.Entities;

namespace TradingJournal.UI.Forms.Base;

// Tek bir kaydı (hesap, enstrüman, strateji, etiket) düzenleyen generic pencere.
// Veritabanına yazmaz; sadece formdaki değerleri nesneye aktarır.
// Kaydetme işini pencereyi açan liste formu yapar.
[DesignerCategory("Code")]
public abstract class EntityEditForm<T> : DialogFormBase where T : BaseEntity
{
    protected T Entity { get; }
    protected bool IsNew => Entity.Id == 0;

    protected EntityEditForm(T entity, string title) : base(title)
    {
        Entity = entity;
    }

    // Nesnedeki değerleri form alanlarına yazar.
    protected abstract void LoadValues();

    // Form alanlarındaki değerleri nesneye yazar. Entity'nin property setter'ları
    // geçersiz değerde DomainException fırlatır; böylece doğrulama tek yerde (Core) kalır.
    protected abstract void ApplyValues();

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // Kurucuda değil burada çağırıyoruz: alt sınıfın kontrolleri artık oluşturulmuş olur.
        LoadValues();
    }

    protected override Task SaveAsync()
    {
        ApplyValues();
        return Task.CompletedTask;
    }
}
