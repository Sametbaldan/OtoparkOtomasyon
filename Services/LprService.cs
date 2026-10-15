using System.Security.Cryptography;

namespace Otopark.Services;

public record LprResult(string Plate, double Confidence, bool Simulated);

/// Gerçek entegrasyon için bu arayüzü uygulayın (Plate Recognizer, Azure AI Vision OCR,
/// OpenALPR vb.) ve Program.cs içindeki kaydı değiştirin.
public interface ILprService
{
    Task<LprResult> ReadAsync(Stream image, string fileName);
}

/// Simülasyon: dosya adı geçerli bir plakaysa (ör. 34ABC123.jpg) onu "okur";
/// aksi halde görüntünün hash'inden deterministik bir plaka üretir.
public class SimulatedLprService : ILprService
{
    private const string Letters = "ABCDEFGHJKLMNPRSTUVYZ";

    public async Task<LprResult> ReadAsync(Stream image, string fileName)
    {
        var name = Plates.Normalize(Path.GetFileNameWithoutExtension(fileName));
        if (Plates.IsValid(name)) return new LprResult(name, 0.98, true);

        using var ms = new MemoryStream();
        await image.CopyToAsync(ms);
        var h = SHA256.HashData(ms.ToArray());
        var city = h[0] % 81 + 1;
        var letters = new string(Enumerable.Range(1, 3).Select(i => Letters[h[i] % Letters.Length]).ToArray());
        var digits = (h[4] * 256 + h[5]) % 900 + 100;
        return new LprResult($"{city:00}{letters}{digits}", Math.Round(0.5 + h[6] / 512.0, 2), true);
    }
}
