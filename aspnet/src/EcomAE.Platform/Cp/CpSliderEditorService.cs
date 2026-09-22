using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

public sealed record CpSliderImage(long Id, int Order, string Href, string Link);

public sealed record CpSliderSettings(int CntImg, int CntImgNext, int TimeNextSeconds, bool Connected);

public sealed record CpSliderPage(CpSliderSettings Settings, IReadOnlyList<CpSliderImage> Images);

public interface ICpSliderEditorService
{
    Task<CpSliderPage> LoadAsync(CancellationToken cancellationToken = default);
}

/// <summary>Read side of the CP slider twin (<c>content/slider/slider.php</c>); writes stay in <see cref="ICpSliderWriteService"/>.</summary>
public sealed class CpSliderEditorService : ICpSliderEditorService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpSliderEditorService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<CpSliderPage> LoadAsync(CancellationToken cancellationToken = default)
    {
        var settings = new CpSliderSettings(1, 1, 5, false);
        var images = new List<CpSliderImage>();
        if (!_connections.IsConfigured)
        {
            return new(settings, images);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT IFNULL(`cnt_img`,1), IFNULL(`cnt_img_next`,1), IFNULL(`time_next`,5000), IFNULL(`connected`,0) FROM `slider_setings` LIMIT 1";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    settings = new CpSliderSettings(
                        Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture) / 1000,
                        Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture) != 0);
                }
            }

            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT `id`, IFNULL(`orders`,0), IFNULL(`href`,''), IFNULL(`link`,'') FROM `slider_images` ORDER BY `orders` ASC, `id` ASC";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    images.Add(new CpSliderImage(
                        Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
                        reader.GetString(2),
                        reader.GetString(3)));
                }
            }
        }
        catch (DbException)
        {
        }

        return new(settings, images);
    }
}
