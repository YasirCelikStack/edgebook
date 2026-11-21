using System.ComponentModel;
using DevExpress.XtraEditors;
using TradingJournal.Core.Entities;
using TradingJournal.Core.Exceptions;
using TradingJournal.Data;
using TradingJournal.Data.Repositories;
using TradingJournal.UI.Forms.Base;
using TradingJournal.UI.Helpers;

namespace TradingJournal.UI.Forms.Trades;

// Açık bir işlemi kapatır. Kapatma işi Trade.Close() metoduyla yapılır:
// kurallar (zaten kapalı mı, tarih sırası doğru mu) entity'nin içinde kalır.
[DesignerCategory("Code")]
public class CloseTradeForm : DialogFormBase
{
    private readonly int _tradeId;
    private readonly TradingJournalDbContext _db = DbContextFactory.Create();
    private Trade? _trade;

    private readonly LabelControl _summary = new() { AutoSizeMode = LabelAutoSizeMode.Vertical };
    private readonly SpinEdit _exitPrice = EditorFactory.Decimal();
    private readonly DateEdit _closeTime = EditorFactory.DateTime();
    private readonly SpinEdit _commission = EditorFactory.Decimal(decimals: 2);

    // Kapanıştan sonra ana pencerede göstermek için sonuç özeti.
    public string? ResultMessage { get; private set; }

    public CloseTradeForm(int tradeId) : base("İşlemi kapat", width: 400)
    {
        _tradeId = tradeId;

        AddField("İşlem", _summary).TextVisible = false;
        AddField("Çıkış fiyatı", _exitPrice);
        AddField("Kapanış zamanı", _closeTime);
        AddField("Komisyon", _commission);
        FitHeight(170);
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        try
        {
            _trade = await new TradeRepository(_db).GetWithDetailsAsync(_tradeId)
                     ?? throw new DomainException("İşlem bulunamadı, silinmiş olabilir.");

            _summary.Text = $"{_trade.Instrument.Symbol}  {_trade.Direction}  |  Giriş: {_trade.EntryPrice}  SL: {_trade.StopLoss}"
                            + (_trade.TakeProfit is decimal tp ? $"  TP: {tp}" : string.Empty);
            _exitPrice.Value = _trade.TakeProfit ?? _trade.EntryPrice;
            _closeTime.EditValue = DateTime.Now;
            _commission.Value = _trade.Commission;
        }
        catch (Exception ex)
        {
            ErrorHelper.Show(this, ex);
            Close();
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _db.Dispose();
        base.OnFormClosed(e);
    }

    protected override async Task SaveAsync()
    {
        if (_trade is null)
            return;
        if (_closeTime.EditValue is not DateTime closeTime)
            throw new DomainException("Kapanış zamanını girmelisiniz.");

        _trade.Commission = _commission.Value;
        _trade.Close(_exitPrice.Value, closeTime);
        await new TradeRepository(_db).UpdateAsync(_trade);

        ResultMessage = $"Net PnL: {_trade.NetPnL:n2} {_trade.Account.Currency}   |   R: {_trade.RMultiple:0.00}";
    }
}
