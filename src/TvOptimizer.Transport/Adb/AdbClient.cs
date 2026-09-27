// AdbClient.cs — мінімальний ADB-over-TCP, написаний з нуля.
// БЕЗ сторонніх ADB-бібліотек: нема чого аудитувати на походження.
// Протокол AOSP adbd: 24 байти LE (cmd,arg0,arg1,len,sum,magic)+payload.
// CNXN -> AUTH(RSA) -> OPEN shell -> WRTE/CLSE.
// Pairing-код вводиться на ТВ вручну (Wireless Debugging -> Pair),
// додаток підключається на ADB-порт (useTls для TLS-порту).
using System.Buffers.Binary;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace TvOptimizer.Transport.Adb;

public sealed class AdbClient : IAsyncDisposable
{
    private const uint CnxN = 0x4E584E43, Auth = 0x48545541;
    private const uint Open = 0x4E45504F, Okay = 0x59414B4F;
    private const uint Clse = 0x45534C43, Wrte = 0x45545257;
    private static readonly byte[] Hello = Encoding.ASCII.GetBytes(
        "host::features=shell_v2,cmd,stat_v2,ls_v2,fixed_push_mkdir,apex,abb,abi");

    private readonly string _host; private readonly int _port;
    private readonly bool _tls; private readonly RSA _rsa;
    private TcpClient? _tcp; private Stream? _s;
    private readonly SemaphoreSlim _wl = new(1, 1);
    private readonly TaskCompletionSource<bool> _authed = new();
    private TaskCompletionSource<bool> _done = new();
    private readonly List<byte> _buf = new();

    public AdbClient(string h, int p, bool tls = false, RSA? k = null)
    { _host = h; _port = p; _tls = tls; _rsa = k ?? RSA.Create(2048); }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _tcp = new TcpClient();
        await _tcp.ConnectAsync(_host, _port, ct);
        Stream s = _tcp.GetStream();
        if (_tls)
        {
            var ssl = new SslStream(s, false, (a, b, c, d) => true);
            await ssl.AuthenticateAsClientAsync(_host);
            s = ssl;
        }
        _s = s;
        _ = Task.Run(ReadLoop);
        await SendAsync(CnxN, 0x01000000, 4096, Hello, ct);
        await _authed.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
    }
    private async Task ReadLoop()
    {
        var s = _s!; var hdr = new byte[24];
        while (true)
        {
            try
            {
                await ReadExactAsync(s, hdr, default);
                uint cmd = BinaryPrimitives.ReadUInt32LittleEndian(hdr.AsSpan(0, 4));
                uint a0 = BinaryPrimitives.ReadUInt32LittleEndian(hdr.AsSpan(4, 4));
                uint a1 = BinaryPrimitives.ReadUInt32LittleEndian(hdr.AsSpan(8, 4));
                uint len = BinaryPrimitives.ReadUInt32LittleEndian(hdr.AsSpan(12, 4));
                byte[] pl = len > 0 ? new byte[len] : Array.Empty<byte>();
                if (len > 0) await ReadExactAsync(s, pl, default);
                if (cmd == Auth && a0 == 1)
                {
                    var sig = _rsa.SignData(pl, HashAlgorithmName.SHA1,
                        RSASignaturePadding.Pkcs1);
                    await SendAsync(Auth, 2, 0, sig);
                }
                else if (cmd == CnxN) _authed.TrySetResult(true);
                else if (cmd == Wrte)
                {
                    await SendAsync(Okay, 1, a0, Array.Empty<byte>());
                    _buf.AddRange(pl);
                }
                else if (cmd == Clse)
                {
                    await SendAsync(Clse, 1, a0, Array.Empty<byte>());
                    _done.TrySetResult(true);
                }
            }
            catch { break; }
        }
    }

    /// <summary>Виконати shell-команду, повернути вивід текстом.</summary>
    public async Task<string> ShellAsync(string c, int tSec = 20,
        CancellationToken ct = default)
    {
        _buf.Clear();
        _done = new TaskCompletionSource<bool>();
        await SendAsync(Open, 1, 0,
            Encoding.UTF8.GetBytes("shell:" + c), ct);
        await _done.Task.WaitAsync(TimeSpan.FromSeconds(tSec), ct);
        return Encoding.UTF8.GetString(_buf.ToArray());
    }

    private async Task SendAsync(uint cmd, uint a0, uint a1,
        byte[] pl, CancellationToken ct = default)
    {
        await _wl.WaitAsync(ct);
        try
        {
            var h = new byte[24];
            BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(0, 4), cmd);
            BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(4, 4), a0);
            BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(8, 4), a1);
            BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(12, 4), (uint)pl.Length);
            uint sum = 0; foreach (var b in pl) sum += b;
            BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(16, 4), sum);
            BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(20, 4), cmd ^ 0xFFFFFFFF);
            await _s!.WriteAsync(h, ct);
            if (pl.Length > 0) await _s.WriteAsync(pl, ct);
            await _s.FlushAsync(ct);
        }
        finally { _wl.Release(); }
    }

    private static async Task ReadExactAsync(Stream s, byte[] b, CancellationToken ct)
    {
        int off = 0;
        while (off < b.Length)
        {
            int n = await s.ReadAsync(b.AsMemory(off), ct);
            if (n == 0) throw new IOException("ADB closed");
            off += n;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try { _s?.Dispose(); _tcp?.Dispose(); } catch { }
        _wl.Dispose();
        await Task.CompletedTask;
    }
}
