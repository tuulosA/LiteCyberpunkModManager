using System;
using System.IO.Pipes;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace CyberpunkModManager.Services
{
    public static class SingleInstanceManager
    {
        private const string MutexName = "CyberpunkModManager_Mutex";
        private const string PipeName = "CyberpunkModManager_NXMPipe";

        private static Mutex? _mutex;
        public static bool IsPrimaryInstance { get; private set; }

        public static bool InitializeAsPrimary()
        {
            _mutex = new Mutex(true, MutexName, out bool isNew);
            IsPrimaryInstance = isNew;
            return isNew;
        }

        public static async Task SendNxmLinkToPrimaryAsync(string link)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                await client.ConnectAsync(1000); // wait max 1s
                using var writer = new StreamWriter(client);
                await writer.WriteLineAsync(link);
                await writer.FlushAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IPC] Failed to send NXM link to main instance: {ex.Message}");
            }
        }

        public static void StartPipeServer(Func<string, Task> onMessageReceived)
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                        await server.WaitForConnectionAsync();

                        using var reader = new StreamReader(server);
                        string? message = await reader.ReadLineAsync();

                        if (!string.IsNullOrEmpty(message))
                        {
                            Debug.WriteLine($"[IPC] Received NXM link: {message}");
                            await onMessageReceived(message);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[IPC] Server error: {ex.Message}");
                    }
                }
            });
        }
    }
}
