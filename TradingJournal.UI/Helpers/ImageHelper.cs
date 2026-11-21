using System.Drawing.Drawing2D;

namespace TradingJournal.UI.Helpers;

// Ekran görüntülerinden küçük önizleme resmi üretir.
// Dosya kilitlenmez (önce belleğe okunur), böylece orijinal dosya sonradan silinebilir.
public static class ImageHelper
{
    public static Image? LoadThumbnail(string path, int maxWidth, int maxHeight)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            using Image original = Image.FromStream(stream);
            double scale = Math.Min(1.0, Math.Min(maxWidth / (double)original.Width, maxHeight / (double)original.Height));
            var thumb = new Bitmap(Math.Max(1, (int)(original.Width * scale)), Math.Max(1, (int)(original.Height * scale)));
            using Graphics g = Graphics.FromImage(thumb);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(original, 0, 0, thumb.Width, thumb.Height);
            return thumb;
        }
        catch (Exception)
        {
            return null; // Dosya silinmiş ya da bozuk.
        }
    }
}
