using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Tailwind
{
    /// <summary>
    /// Lance "npm run buildcss:watch" en arrière-plan pendant le dev
    /// et relaye la sortie dans la console .NET. Tue le process à l'arrêt.
    /// </summary>
    public static class RunTailwindExtensions
    {
        public static void RunTailwind(this WebApplication app, string script = "buildcss:watch", string? workingDir = null)
        {
            if (!app.Environment.IsDevelopment()) return;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = OperatingSystem.IsWindows() ? "cmd" : "/bin/bash",
                    Arguments = OperatingSystem.IsWindows()
                        ? $"/c npm run {script}"
                        : $"-lc \"npm run {script}\"",
                    WorkingDirectory = workingDir ?? app.Environment.ContentRootPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var proc = Process.Start(psi);
                if (proc == null) return;

                // Arrêt propre quand l’appli s’éteint
                app.Lifetime.ApplicationStopping.Register(() =>
                {
                    try { if (!proc.HasExited) proc.Kill(true); } catch { /* ignore */ }
                });

                // Relayer la sortie pour la voir dans le terminal Dotnet
                _ = Task.Run(() => Relay(proc.StandardOutput, "NodeServices"));
                _ = Task.Run(() => Relay(proc.StandardError, "NodeServices"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Tailwind] Watch non démarré : {ex.Message}");
            }
        }

        private static async Task Relay(StreamReader reader, string prefix)
        {
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                Console.WriteLine($"{prefix}[0]  {line}");
            }
        }
    }
}
