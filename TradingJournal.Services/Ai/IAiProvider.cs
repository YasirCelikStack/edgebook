namespace TradingJournal.Services.Ai;

// Sohbetteki tek mesaj. Role: kullanıcı mı yoksa model mi yazdı.
public record ChatMessage(bool FromUser, string Text);

// Yapay zeka sağlayıcısı sözleşmesi. Koç servisi hangi şirketin modelini kullandığını bilmez;
// bugün Gemini, yarın Claude veya yerel bir model (Ollama) aynı arayüzle takılabilir.
public interface IAiProvider
{
    string Name { get; }

    // systemPrompt: modelin rolü ve kuralları. messages: sırayla sohbet (sonuncusu kullanıcının sorusu).
    Task<string> GenerateAsync(string systemPrompt, IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken = default);
}
