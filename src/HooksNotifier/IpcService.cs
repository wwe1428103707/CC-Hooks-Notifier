using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace HooksNotifier;

/// <summary>
/// Named pipe IPC between --hook (client) and --tray (server) modes.
/// Pipe name: ClaudeCodeHooks
/// </summary>
internal static class IpcService
{
    private const string PipeName = "ClaudeCodeHooks";
    private const int ClientTimeoutMs = 1000;

    // ── Client (called from --hook mode) ──────────────────────────────

    /// <summary>Send an arbitrary IPC message to the tray process.</summary>
    public static bool Send(IpcMessage message)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
            pipe.Connect(ClientTimeoutMs);

            var json = JsonSerializer.Serialize(message, JsonOpts.Default);
            var buffer = Encoding.UTF8.GetBytes(json + "\n");
            pipe.Write(buffer, 0, buffer.Length);
            pipe.Flush();

            // Read response
            using var reader = new StreamReader(pipe, Encoding.UTF8);
            var response = reader.ReadLine();

            return true;
        }
        catch (TimeoutException)
        {
            return false; // Tray not running
        }
        catch (FileNotFoundException)
        {
            return false; // Pipe server not created
        }
    }

    // ── Server (called from --tray mode) ───────────────────────────────

    /// <summary>
    /// Start the named pipe server on a background thread.
    /// Calls onMessage on the caller's synchronization context for each received message.
    /// </summary>
    public static void StartServer(Action<IpcMessage> onMessage, CancellationToken ct)
    {
        Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut,
                        1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                    await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);

                    using var reader = new StreamReader(pipe, Encoding.UTF8);
                    var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);

                    if (string.IsNullOrEmpty(line)) continue;

                    var msg = JsonSerializer.Deserialize<IpcMessage>(line, JsonOpts.Default);
                    if (msg != null)
                    {
                        // Log receipt for debugging
                        System.Diagnostics.Debug.WriteLine($"IPC recv: {msg.Type}/{msg.EventName}");
                        onMessage(msg);
                    }

                    // Send response
                    var response = JsonSerializer.Serialize(new IpcResponse { Status = "ok" }, JsonOpts.Default);
                    var respBytes = Encoding.UTF8.GetBytes(response + "\n");
                    await pipe.WriteAsync(respBytes, ct).ConfigureAwait(false);
                    await pipe.FlushAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    if (!ex.Message.Contains("Pipe is broken"))
                        Log.Error($"IPC server: {ex.Message}");
                    await Task.Delay(100, ct).ConfigureAwait(false);
                }
            }
        }, ct);
    }
}
