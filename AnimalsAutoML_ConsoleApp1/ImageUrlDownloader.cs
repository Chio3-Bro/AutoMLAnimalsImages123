using System.Net;
using System.Net.Sockets;
namespace AnimalsAutoML_ConsoleApp1;

public interface IImageUrlDownloader
{
    Task<byte[]> DownloadAsync(string url, CancellationToken cancellationToken);
}

public sealed class ImageUrlDownloader : IImageUrlDownloader
{
    public static Uri ValidateUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || !uri.IsDefaultPort || uri.UserInfo.Length != 0)
            throw new InvalidDataException("Надішліть пряме HTTP/HTTPS-посилання на зображення без логіна та пароля.");
        return uri;
    }

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var b = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return (b[0] & 0xe0) == 0x20 && !(b[0] == 0x20 && b[1] == 0x01 && b[2] < 0x20)
                && !(b[0] == 0x20 && b[1] == 0x02) && !(b[0] == 0x3f && b[1] == 0xff);
        return b[0] is not (0 or 10 or 127) && b[0] < 224
            && !(b[0] == 100 && b[1] is >= 64 and <= 127)
            && !(b[0] == 169 && b[1] == 254)
            && !(b[0] == 172 && b[1] is >= 16 and <= 31)
            && !(b[0] == 192 && (b[1] is 0 or 168 || b[1] == 88 && b[2] == 99))
            && !(b[0] == 198 && (b[1] is 18 or 19 || b[1] == 51 && b[2] == 100))
            && !(b[0] == 203 && b[1] == 0 && b[2] == 113);
    }

    public async Task<byte[]> DownloadAsync(string url, CancellationToken cancellationToken)
    {
        var uri = ValidateUrl(url);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false, UseProxy = false, UseCookies = false,
            ConnectCallback = async (context, token) =>
            {
                var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, token);
                if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
                    throw new HttpRequestException("Non-public destination blocked");
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(addresses[0], context.DnsEndPoint.Port), token);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch { socket.Dispose(); throw; }
            }
        };
        using var client = new HttpClient(handler);
        try
        {
            for (var redirects = 0; redirects <= 3; redirects++)
            {
                using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
                {
                    var location = response.Headers.Location;
                    if (location is null || redirects == 3)
                        throw new InvalidDataException("Забагато перенаправлень або некоректне посилання.");
                    uri = ValidateUrl(new Uri(uri, location).AbsoluteUri);
                    continue;
                }
                if (!response.IsSuccessStatusCode)
                    throw new InvalidDataException("Не вдалося відкрити посилання. Зображення має бути доступним без авторизації.");
                if (response.Content.Headers.ContentLength > 5 * 1024 * 1024)
                    throw new InvalidDataException("Фото перевищує 5 МіБ.");
                await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
                return await ReadLimitedAsync(input, timeout.Token);
            }
            throw new InvalidDataException("Не вдалося завантажити зображення.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidDataException("Завантаження за URL тривало надто довго. Спробуйте інше посилання.");
        }
        catch (HttpRequestException)
        {
            throw new InvalidDataException("Не вдалося завантажити URL. Використайте доступне публічне посилання на JPEG або PNG.");
        }
        catch (IOException)
        {
            throw new InvalidDataException("Завантаження перервано. Спробуйте ще раз або надішліть інше посилання.");
        }
    }

    public static async Task<byte[]> ReadLimitedAsync(Stream input, CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (output.Length + count > 5 * 1024 * 1024)
                throw new InvalidDataException("Фото перевищує 5 МіБ.");
            output.Write(buffer, 0, count);
        }
        if (output.Length == 0) throw new InvalidDataException("За посиланням отримано порожній файл.");
        return output.ToArray();
    }
}
