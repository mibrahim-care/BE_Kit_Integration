using System.Net;
using System.Net.Security;
using System.Net.Sockets;

namespace Fakeeh.MedicalKit.Infrastructure.His;

internal static class HisHttpHandler
{
    public static SocketsHttpHandler Create(HisOptions options)
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectCallback = (context, cancellationToken) =>
                ConnectToFirstReachableAsync(context.DnsEndPoint, TimeSpan.FromSeconds(options.ConnectTimeoutSeconds), cancellationToken)
        };

        if (options.IgnoreSslErrors)
        {
            handler.SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            };
        }

        return handler;
    }

    private static async ValueTask<Stream> ConnectToFirstReachableAsync(
        DnsEndPoint endPoint,
        TimeSpan attemptTimeout,
        CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(endPoint.Host, cancellationToken);
        if (addresses.Length == 0)
        {
            throw new HttpRequestException($"No IP address found for '{endPoint.Host}'.");
        }

        using var raceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        raceCts.CancelAfter(attemptTimeout);

        var attempts = addresses.Select(address => ConnectAsync(address, endPoint.Port, raceCts.Token)).ToList();
        var failures = new List<Exception>();

        while (attempts.Count > 0)
        {
            var finished = await Task.WhenAny(attempts);
            attempts.Remove(finished);

            if (finished.IsCompletedSuccessfully)
            {
                await raceCts.CancelAsync();
                _ = DisposeLosersAsync(attempts);
                return new NetworkStream(finished.Result, ownsSocket: true);
            }

            failures.Add(finished.Exception?.GetBaseException() ?? new OperationCanceledException());
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new HttpRequestException(
            $"Could not connect to {endPoint.Host}:{endPoint.Port} on any of {addresses.Length} addresses.",
            new AggregateException(failures));
    }

    private static async Task<Socket> ConnectAsync(IPAddress address, int port, CancellationToken cancellationToken)
    {
        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static async Task DisposeLosersAsync(IEnumerable<Task<Socket>> attempts)
    {
        foreach (var attempt in attempts)
        {
            try
            {
                (await attempt).Dispose();
            }
            catch
            {
            }
        }
    }
}
