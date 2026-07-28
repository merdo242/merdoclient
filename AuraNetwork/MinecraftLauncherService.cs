using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;

namespace AuraNetwork;

public class MinecraftLauncherService
{
    private readonly SettingsService _settingsService;

    // Sunucuyla uyumlu sabit sürüm
    public const string FixedVersion = "1.21.8";

    public MinecraftLauncherService(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>Minecraft'ı başlatır. Arka planda (Task.Run içinde) çağrılmalıdır.</summary>
    private static readonly string[] CheatKeywords = new[]
    {
        "xray", "x-ray", "wurst", "meteor", "aristois", "liquidbounce", 
        "bleachhack", "kappahack", "inertia", "mathax", "matix", "impact", 
        "sigma", "vape", "raven", "skillclient", "wolfram", "jigsaw", 
        "flux", "flare", "huzuni", "nodus", "autoclicker", "macro"
    };

    private void ScanForCheats(string modsDir, Action<string>? onProgress)
    {
        if (!Directory.Exists(modsDir)) return;
        
        onProgress?.Invoke("Anti-Cheat taraması yapılıyor...");
        
        var files = Directory.GetFiles(modsDir, "*.jar", SearchOption.AllDirectories);
        bool cheatFound = false;
        
        foreach (var file in files)
        {
            string fileName = Path.GetFileName(file).ToLowerInvariant();
            
            foreach (var keyword in CheatKeywords)
            {
                if (fileName.Contains(keyword))
                {
                    try
                    {
                        File.Delete(file);
                        cheatFound = true;
                        System.Windows.Forms.MessageBox.Show(
                            "Güvenlik Koruması:\nSisteminizde yasaklı bir hile modu tespit edildi ve otomatik olarak silindi!\nSilinen dosya: " + Path.GetFileName(file),
                            "AuraNW Anti-Cheat",
                            System.Windows.Forms.MessageBoxButtons.OK,
                            System.Windows.Forms.MessageBoxIcon.Warning);
                    }
                    catch { }
                    break;
                }
            }
        }
        
        if (cheatFound)
        {
            onProgress?.Invoke("Hile dosyaları başarıyla temizlendi!");
            System.Threading.Thread.Sleep(1500);
        }
    }

    public LauncherResult StartMinecraft(string username, string password, Action<string>? onProgress = null)
    {
        if (string.IsNullOrWhiteSpace(username))
            return new LauncherResult(false, "Minecraft'ı başlatmak için kullanıcı adı gereklidir.");

        try
        {
            var path     = new MinecraftPath();
            var launcher = new MinecraftLauncher(path);
            var settings = _settingsService.Settings;

            // --- İndirme durumunu bildiren olaylar ---
            launcher.FileProgressChanged += (sender, e) =>
            {
                onProgress?.Invoke("Oyun dosyaları indiriliyor ve doğrulanıyor...");
            };

            // --- Orijinal Temiz Kurulum (Fabric + Sodium 1.21.1) ---
            string versionsDir = Path.Combine(path.BasePath, "versions");
            string? fabricVerDir = Directory.Exists(versionsDir) 
                ? Directory.GetDirectories(versionsDir, "fabric-loader-*-1.21.1").FirstOrDefault() 
                : null;
                
            bool needsDownload = string.IsNullOrEmpty(fabricVerDir) || !Directory.Exists(Path.Combine(path.BasePath, "libraries", "net", "fabricmc"));
            
            if (needsDownload)
            {
                onProgress?.Invoke("Orijinal OptiFine (Fabric+Sodium) Paketi indiriliyor... Lütfen bekleyin.");
                try 
                {
                    using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(10) };
                    var zipBytes = client.GetByteArrayAsync("https://github.com/merdo242/auranetwork/raw/main/installer/Merdo_Fabric_1.21.1.zip").GetAwaiter().GetResult();
                    var tempZip = Path.Combine(Path.GetTempPath(), "Merdo_Fabric_1.21.1.zip");
                    System.IO.File.WriteAllBytes(tempZip, zipBytes);
                    System.IO.Compression.ZipFile.ExtractToDirectory(tempZip, path.BasePath, true);
                    System.IO.File.Delete(tempZip);
                    onProgress?.Invoke("Orijinal sürüm ve modlar başarıyla kuruldu.");
                } 
                catch (Exception ex) 
                {
                    return new LauncherResult(false, "Sürüm indirilemedi: " + ex.Message + "\nİnternet bağlantınızı kontrol edin.");
                }
            }

            // --- Sesli Sohbet (Simple Voice Chat) modunu otomatik kur ---
            string modsDir = Path.Combine(path.BasePath, "mods");
            if (!Directory.Exists(modsDir)) Directory.CreateDirectory(modsDir);

            ScanForCheats(modsDir, onProgress);

            string voiceChatName = "voicechat-fabric-1.21.1-2.6.20.jar";
            string voiceChatPath = Path.Combine(modsDir, voiceChatName);
            if (!File.Exists(voiceChatPath))
            {
                // Silinmesi gereken eski sürümler varsa (örneğin kullanıcının attığı yanlış sürüm)
                var oldVersions = Directory.GetFiles(modsDir, "voicechat-fabric-*.jar");
                foreach (var old in oldVersions)
                {
                    try { File.Delete(old); } catch { }
                }

                onProgress?.Invoke("Sesli sohbet modu (Simple Voice Chat) indiriliyor...");
                try
                {
                    using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(2) };
                    var voiceBytes = client.GetByteArrayAsync("https://cdn.modrinth.com/data/9eGKb6K1/versions/IttovdN3/voicechat-fabric-1.21.1-2.6.20.jar").GetAwaiter().GetResult();
                    File.WriteAllBytes(voiceChatPath, voiceBytes);
                }
                catch
                {
                    // Hata olursa en azından oyuna devam etsin, sessiz kalalım
                }
            }
            
            // --- First-Person Model Modunu kur (Punchy yerine) ---
            string firstPersonName = "firstperson-fabric-2.4.4-mc1.21.1.jar";
            string firstPersonPath = Path.Combine(modsDir, firstPersonName);
            if (!File.Exists(firstPersonPath) && !File.Exists(firstPersonPath + ".disabled"))
            {
                // Eski sürümleri (veya punchy'yi) sil
                var oldFPMVersions = Directory.GetFiles(modsDir, "firstperson-*.jar").Concat(Directory.GetFiles(modsDir, "punchy-*.jar"));
                foreach (var old in oldFPMVersions)
                {
                    try { File.Delete(old); } catch { }
                }
                
                // .disabled punchy dosyalari varsa temizle
                var disabledPunchy = Directory.GetFiles(modsDir, "punchy-*.disabled");
                foreach (var old in disabledPunchy) { try { File.Delete(old); } catch { } }

                onProgress?.Invoke("First-Person Model indiriliyor...");
                try
                {
                    using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(2) };
                    var fpmBytes = client.GetByteArrayAsync("https://cdn.modrinth.com/data/joPjFTev/versions/S5e9J0xU/firstperson-fabric-2.4.4-mc1.21.1.jar").GetAwaiter().GetResult();
                    File.WriteAllBytes(firstPersonPath, fpmBytes);
                }
                catch { }
            }

            // --- Chat Animation Modunu kur ---
            string chatAnimName = "chatanimation-fabric-1.3.1+mc1.21.jar";
            string chatAnimPath = Path.Combine(modsDir, chatAnimName);
            if (!File.Exists(chatAnimPath) && !File.Exists(chatAnimPath + ".disabled"))
            {
                var oldChatAnimVersions = Directory.GetFiles(modsDir, "chatanimation-*.jar");
                foreach (var old in oldChatAnimVersions) { try { File.Delete(old); } catch { } }
                onProgress?.Invoke("Chat Animation modu indiriliyor...");
                try
                {
                    using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(2) };
                    var bytes = client.GetByteArrayAsync("https://cdn.modrinth.com/data/DnNYdJsx/versions/4jzApw0F/chatanimation-fabric-1.3.1%2Bmc1.21.jar").GetAwaiter().GetResult();
                    File.WriteAllBytes(chatAnimPath, bytes);
                }
                catch { }
            }

            // --- Chat Heads Modunu kur ---
            string chatHeadsName = "chat_heads-0.15.2-fabric-1.21.jar";
            string chatHeadsPath = Path.Combine(modsDir, chatHeadsName);
            if (!File.Exists(chatHeadsPath) && !File.Exists(chatHeadsPath + ".disabled"))
            {
                var oldChatHeadsVersions = Directory.GetFiles(modsDir, "chat_heads-*.jar");
                foreach (var old in oldChatHeadsVersions) { try { File.Delete(old); } catch { } }
                onProgress?.Invoke("Chat Heads modu indiriliyor...");
                try
                {
                    using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(2) };
                    var bytes = client.GetByteArrayAsync("https://cdn.modrinth.com/data/Wb5oqrBJ/versions/O34Q4oXE/chat_heads-0.15.2-fabric-1.21.jar").GetAwaiter().GetResult();
                    File.WriteAllBytes(chatHeadsPath, bytes);
                }
                catch { }
            }

            // --- CraterLib (Simple RPC için gerekli) ---
            string craterLibName = "CraterLib-Fabric-1.21-3.1.2.jar";
            string craterLibPath = Path.Combine(modsDir, craterLibName);
            if (!File.Exists(craterLibPath) && !File.Exists(craterLibPath + ".disabled"))
            {
                var oldCraterLibVersions = Directory.GetFiles(modsDir, "CraterLib-*.jar");
                foreach (var old in oldCraterLibVersions) { try { File.Delete(old); } catch { } }
                onProgress?.Invoke("CraterLib (RPC kütüphanesi) indiriliyor...");
                try
                {
                    using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(2) };
                    var bytes = client.GetByteArrayAsync("https://cdn.modrinth.com/data/Nn8Wasaq/versions/7vCrReSb/CraterLib-Fabric-1.21-3.1.2.jar").GetAwaiter().GetResult();
                    File.WriteAllBytes(craterLibPath, bytes);
                }
                catch { }
            }

            // --- Simple RPC Modunu kur ---
            string rpcName = "SimpleRPC-4.1.2.jar";
            string rpcPath = Path.Combine(modsDir, rpcName);
            if (!File.Exists(rpcPath) && !File.Exists(rpcPath + ".disabled"))
            {
                var oldRpcVersions = Directory.GetFiles(modsDir, "SimpleRPC-*.jar");
                foreach (var old in oldRpcVersions) { try { File.Delete(old); } catch { } }
                onProgress?.Invoke("Discord RPC modu indiriliyor...");
                try
                {
                    using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(2) };
                    var bytes = client.GetByteArrayAsync("https://cdn.modrinth.com/data/ObXSoyrn/versions/K1WFXmS7/SimpleRPC-4.1.2.jar").GetAwaiter().GetResult();
                    File.WriteAllBytes(rpcPath, bytes);
                }
                catch { }
            }

            // Simple RPC Konfigürasyonunu oluştur (v4 formatında)
            try
            {
                string rpcConfigDir = Path.Combine(path.BasePath, "config", "simple-rpc");
                if (!Directory.Exists(rpcConfigDir)) Directory.CreateDirectory(rpcConfigDir);
                
                string rpcConfigFile = Path.Combine(rpcConfigDir, "simple-rpc.toml");
                
                string tomlContent = """
                    appID = "1524029476084781126"
                    version = 33

                    [main_menu]
                        enabled = true
                        [[main_menu.presence]]
                            type = "PLAYING"
                            description = "AuraNW Client ile oynuyor"
                            state = "Ana Menü'de"
                            largeImageKey = ["logo"]
                            largeImageText = "Aura Network"
                            buttons = []

                    [multi_player]
                        enabled = true
                        [[multi_player.presence]]
                            type = "PLAYING"
                            description = "AuraNW Client ile oynuyor"
                            state = "Oyunda"
                            largeImageKey = ["logo"]
                            largeImageText = "Aura Network"
                            buttons = []
                            
                    [single_player]
                        enabled = true
                        [[single_player.presence]]
                            type = "PLAYING"
                            description = "AuraNW Client ile oynuyor"
                            state = "Oyunda"
                            largeImageKey = ["logo"]
                            largeImageText = "Aura Network"
                            buttons = []
                            
                    [generic]
                        [[generic.presence]]
                            type = "PLAYING"
                            description = "AuraNW Client ile oynuyor"
                            state = ""
                            largeImageKey = ["logo"]
                            largeImageText = "Aura Network"
                            buttons = []
                    """;
                File.WriteAllText(rpcConfigFile, tomlContent);
            }
            catch { }

            // --- AuraNWBridge Modunu kur ---
            var oldMods = new[] { "auranetwork-*.jar", "merdobridge-*.jar", "chickenclient-*.jar" };
            foreach (var pattern in oldMods)
            {
                var files = Directory.GetFiles(modsDir, pattern);
                foreach (var old in files)
                {
                    try { File.Delete(old); } catch { }
                }
            }
// --- Eski CIT Resewn sil ---
            var badMods = new[] { "citresewn-*.jar" };
            foreach (var pattern in badMods)
            {
                var files = Directory.GetFiles(modsDir, pattern);
                foreach (var bad in files)
                {
                    try { File.Delete(bad); } catch { }
                }
            }

            // --- Polytone Modunu kur (CIT ve daha iyi OptiFine destegi icin) ---
            string polytoneName = "polytone-fabric-1.21-3.8.8.jar";
            string polytonePath = Path.Combine(modsDir, polytoneName);
            if (!File.Exists(polytonePath))
            {
                try
                {
                    using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(2) };
                    var bytes = client.GetByteArrayAsync("https://cdn.modrinth.com/data/3qAYkBMB/versions/LWb7tKC7/polytone-fabric-1.21-3.8.8.jar").GetAwaiter().GetResult();
                    File.WriteAllBytes(polytonePath, bytes);
                }
                catch { }
            }

            string bridgeName = "auranetwork-1.1.3.jar";
            string bridgePath = Path.Combine(modsDir, bridgeName);

            // bridge var mı kontrolü, yoksa indir
            if (!File.Exists(bridgePath))
            {
                onProgress?.Invoke("AuraNetwork Bridge (v1.1.3) indiriliyor...");
                using var client = new System.Net.Http.HttpClient();
                try {
                    var bridgeBytes = client.GetByteArrayAsync("https://github.com/merdo242/auranetwork/raw/main/installer/auranetwork-1.1.3.jar").GetAwaiter().GetResult();
                    File.WriteAllBytes(bridgePath, bridgeBytes);
                }
                catch { }
            }

            // --- Kurulan Fabric sürümünü bul ---
            fabricVerDir = Directory.GetDirectories(versionsDir, "fabric-loader-*-1.21.1").FirstOrDefault();
            string launchVersion = string.IsNullOrEmpty(fabricVerDir) ? FixedVersion : Path.GetFileName(fabricVerDir);

            // --- Java yolunu bul ---
            onProgress?.Invoke("Java kontrol ediliyor...");
            string javaPath = !string.IsNullOrEmpty(settings.JavaPath) && File.Exists(settings.JavaPath)
                ? settings.JavaPath
                : FindSystemJavaPath();

            if (string.IsNullOrEmpty(javaPath))
                return new LauncherResult(false,
                    "Java bulunamadı!\n\n" +
                    "Ayarlar menüsünden Java yolunu manuel olarak seçin.\n\n" +
                    "Tipik konum:\n" +
                    @"%AppData%\.minecraft\runtime\java-runtime-delta\windows\java-runtime-delta\bin\javaw.exe");

            // --- Eksik dosyaları zorla indir ---
            onProgress?.Invoke("Eksik oyun dosyaları doğrulanıp indiriliyor... (Bu işlem ilk açılışta sürebilir)");
            launcher.InstallAsync(launchVersion).GetAwaiter().GetResult();

            // --- Session ve başlatma seçenekleri ---
            var session = new MSession(username, "offline_token", Guid.NewGuid().ToString("N"));
            var launchOptions = new MLaunchOption
            {
                // MaximumRamMb = settings.MaxRamMb, // RAM Sınırını kaldırıyoruz
                Session      = session,
                JavaPath     = javaPath
            };

            // Process'i oluştur (Task.Run içinde çalıştığından deadlock yok)
            onProgress?.Invoke("Minecraft dosyaları hazırlanıyor...");
            var process = launcher.CreateProcessAsync(launchVersion, launchOptions).GetAwaiter().GetResult();
            
            // javaw.exe yerine java.exe kullanalım ki hata çıktılarını okuyabilelim
            string exePath = javaPath;
            if (exePath.EndsWith("javaw.exe", StringComparison.OrdinalIgnoreCase))
            {
                string javaExePath = exePath.Substring(0, exePath.Length - 5) + ".exe";
                if (System.IO.File.Exists(javaExePath))
                {
                    exePath = javaExePath;
                }
            }
            process.StartInfo.FileName = exePath;

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.EnvironmentVariables["AURA_TOKEN"] = password;

            // --- Mod Yöneticisi: Devre Dışı Bırakılan Modları Uygula ---
            string disabledModsFile = Path.Combine(path.BasePath, "disabled_mods.txt");
            if (File.Exists(disabledModsFile))
            {
                try
                {
                    var disabledModsList = new System.Collections.Generic.HashSet<string>(File.ReadAllLines(disabledModsFile).Select(l => l.Trim()));
                    var allMods = Directory.GetFiles(modsDir, "*.*").Where(f => f.EndsWith(".jar") || f.EndsWith(".disabled"));
                    foreach (var modFile in allMods)
                    {
                        string fileName = Path.GetFileName(modFile);
                        if (fileName.Contains("auranetwork") || fileName.Contains("merdobridge") || fileName.Contains("chickenclient") || fileName.Contains("fabric-api"))
                            continue;

                        string modKey = fileName.Replace(".jar", "").Replace(".disabled", "");
                        bool shouldBeDisabled = disabledModsList.Contains(modKey);
                        bool isCurrentlyDisabled = fileName.EndsWith(".disabled");

                        if (shouldBeDisabled && !isCurrentlyDisabled)
                        {
                            File.Move(modFile, Path.Combine(modsDir, modKey + ".disabled"));
                        }
                        else if (!shouldBeDisabled && isCurrentlyDisabled)
                        {
                            File.Move(modFile, Path.Combine(modsDir, modKey + ".jar"));
                        }
                    }
                }
                catch { }
            }

            process.Start();

            // Wait a short amount of time to see if it crashes immediately (e.g. Java version mismatch)
            if (process.WaitForExit(2000))
            {
                string errorOutput = process.StandardError.ReadToEnd();
                return new LauncherResult(false, 
                    $"Minecraft aniden kapandı. Oyun desteklenmeyen bir Java sürümüyle açılmaya çalışılmış olabilir.\n" +
                    $"Hata detayı: {errorOutput}\n\n" +
                    "Lütfen ayarlar menüsünden Java 21 yolunu seçtiğinizden emin olun.");
            }

            if (settings.CloseOnLaunch)
            {
                System.Windows.Forms.Application.Exit();
            }

            return new LauncherResult(true, $"Minecraft {launchVersion} başarıyla başlatıldı!");
        }
        catch (Exception ex)
        {
            return new LauncherResult(false,
                $"Minecraft başlatılamadı:\n{ex.Message}\n\n" +
                "• Minecraft Launcher ile en az bir kez oynamış olduğunuzdan emin olun\n" +
                "• Ayarlar'dan Java yolunu manuel seçin");
        }
    }

    /// <summary>Sistemde kurulu javaw.exe'yi arar.</summary>
    public static string FindSystemJavaPath()
    {
        // 1) Minecraft'ın kendi runtime dizini — ÖNCE ARANIR (Java 21/17 Öncelikli)
        string appData   = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string mcRuntime = Path.Combine(appData, ".minecraft", "runtime");
        if (Directory.Exists(mcRuntime))
        {
            try
            {
                // Önce java-runtime-delta (Java 21 - MC 1.20.5+ için) veya gamma (Java 17) ara
                string[] preferredRuntimes = { "java-runtime-delta", "java-runtime-gamma", "java-runtime-beta" };
                foreach (var pref in preferredRuntimes)
                {
                    var prefDirs = Directory.GetDirectories(mcRuntime, pref, SearchOption.AllDirectories);
                    foreach (var pDir in prefDirs)
                    {
                        var jPath = Path.Combine(pDir, "bin", "javaw.exe");
                        if (File.Exists(jPath)) return jPath;
                    }
                }

                // Bulunamazsa herhangi bir javaw.exe (legacy vb.)
                foreach (var found in Directory.EnumerateFiles(mcRuntime, "javaw.exe", SearchOption.AllDirectories))
                    return found;
            }
            catch { }
        }

        // 2) JAVA_HOME
        string? javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(javaHome))
        {
            string c = Path.Combine(javaHome, "bin", "javaw.exe");
            if (File.Exists(c)) return c;
        }

        // 3) PATH
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var dir in pathEnv.Split(';'))
            {
                string c = Path.Combine(dir.Trim(), "javaw.exe");
                if (File.Exists(c)) return c;
            }
        }

        // 4) Bilinen Java kurulum dizinleri
        string pf   = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        foreach (var root in new[]
        {
            Path.Combine(pf,   "Eclipse Adoptium"),
            Path.Combine(pf,   "Java"),
            Path.Combine(pf,   "Microsoft"),
            Path.Combine(pf,   "BellSoft"),
            Path.Combine(pf,   "Zulu"),
            Path.Combine(pf86, "Java"),
            Path.Combine(pf86, "Eclipse Adoptium"),
        })
        {
            if (!Directory.Exists(root)) continue;
            try
            {
                foreach (var found in Directory.EnumerateFiles(root, "javaw.exe", SearchOption.AllDirectories))
                    return found;
            }
            catch { }
        }

        return string.Empty;
    }

    /// <summary>.minecraft/versions klasöründeki 1.21.8 Optifine sürümlerini arar</summary>
    private static string? FindOptifineVersion(string basePath)
    {
        string versionsDir = Path.Combine(basePath, "versions");
        if (!Directory.Exists(versionsDir)) return null;

        try
        {
            var dirs = Directory.GetDirectories(versionsDir);
            // ForgeOptiFine 1.21.8 veya OptiFine 1.21.8 gibi klasörleri bul
            foreach (var d in dirs)
            {
                string name = Path.GetFileName(d);
                if (name.Contains("1.21.8") && name.IndexOf("OptiFine", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return name;
                }
            }
        }
        catch { }

        return null;
    }
}

public sealed record LauncherResult(bool Success, string Message);











