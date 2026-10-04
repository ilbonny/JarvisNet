using PiperSharp;

var targetRoot = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
Directory.SetCurrentDirectory(targetRoot);
await PiperDownloader.DownloadPiper().ExtractPiper(targetRoot).ConfigureAwait(false);
Console.WriteLine($"Piper estratto sotto: {Path.Combine(targetRoot, "piper")}");
