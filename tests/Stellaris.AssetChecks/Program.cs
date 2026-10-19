using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Stellaris.Core.Assets;

if (args.Length != 0 && (args.Length != 2 || args[0] != "--game-root" || string.IsNullOrWhiteSpace(args[1])))
{
    Console.Error.WriteLine("Использование: Stellaris.AssetChecks [--game-root PATH].");
    return 2;
}
var checks = new List<(string Name, Func<Task> Run)>
{
    ("Canonical bytes/digest и ordinal sorting", Canonical),
    ("Parity of 379 paths with the browser profile", Parity),
    ("Synthetic builder, immutable DTO и privacy", Synthetic),
    ("Версия, ошибки, лимиты и cancellation", Failures),
    ("Symlink reparse points отклоняются", Reparse),
    ("Неизменённый источник проходит internal seam", SeamControl),
    ("Same-length mutation между hash-pass отклоняется", SeamMutation),
    ("Отмена в internal seam освобождает handles", SeamCancellation),
    ("Windows junction root/ancestor/internal-path", Junctions)
};
if (args.Length == 2)
{
    checks.Add(("Реальная read-only установка 4.5.2", () => Real(args[1])));
}
var failed = 0;
var skipped = 0;
foreach (var (name, run) in checks)
{
    try
    {
        await run();
        Console.WriteLine("PASS: " + name);
    }
    catch (CheckSkippedException exception)
    {
        skipped++;
        Console.WriteLine($"SKIP: {name}: {exception.Message}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAIL: {name}\n{exception}");
    }
}
Console.WriteLine($"Наборов: {checks.Count}; ошибок: {failed}; пропущено: {skipped}.");
return failed == 0 ? 0 : 1;

static Task Canonical()
{
    var zero = new string('0', 64);
    var one = new string('1', 64);
    var records = new[] { new AssetManifestFile("gfx/Я.dds", 2, zero, "dds"), new AssetManifestFile("gfx/Z.dds", 3, one, "dds") };
    var expected = $"профиль\n4.5.2\n1\n1\n1\ngfx/Z.dds\t3\t{one}\ngfx/Я.dds\t2\t{zero}\n";
    var culture = CultureInfo.CurrentCulture;
    try
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
        Assert(AssetManifestCanonical.GetBytes("профиль", "4.5.2", 1, 1, 1, records).SequenceEqual(Encoding.UTF8.GetBytes(expected)), "Canonical bytes отличаются.");
        Assert(AssetManifestCanonical.ComputeDigest("профиль", "4.5.2", 1, 1, 1, records) == "00c1ebc79ec6a38a39a1cfc70b269e73eac6431f49a77853fd80f3ff84279807", "Golden digest отличается.");
    }
    finally
    {
        CultureInfo.CurrentCulture = culture;
    }
    foreach (var path in new[] { "../outside.dds", "/absolute.dds", "C:/secret.dds", "gfx\\a.dds", "gfx//a.dds", "gfx/./a.dds", "gfx/a\t.dds", "gfx/a\n.dds", "gfx/a\r.dds" })
    {
        Throws<AssetManifestException>(() => new AssetManifestFile(path, 1, zero, "dds"));
    }
    Throws<AssetManifestException>(() => new AssetManifestFile("gfx/a.dds", -1, zero, "dds"));
    Throws<AssetManifestException>(() => new AssetManifestFile("gfx/a.dds", 1, new string('A', 64), "dds"));
    Throws<AssetManifestException>(() => new AssetManifestFile("gfx/a.dds", 1, zero, "shader"));
    Throws<AssetManifestException>(() => AssetManifestCanonical.GetBytes("p", "v", 1, 1, 1, [records[0], records[0]]));
    Throws<AssetManifestException>(() => AssetManifestCanonical.GetBytes("p\n", "v", 1, 1, 1, records));
    var baseline = AssetManifestCanonical.ComputeDigest("p", "v", 1, 1, 1, records);
    foreach (var changed in new[] { new AssetManifestFile("gfx/Я.dds", 3, zero, "dds"), new AssetManifestFile("gfx/Я.dds", 2, one, "dds") })
    {
        Assert(AssetManifestCanonical.ComputeDigest("p", "v", 1, 1, 1, [changed, records[1]]) != baseline, "Изменение длины/hash не изменило canonical digest.");
    }
    return Task.CompletedTask;
}

static async Task Parity()
{
    var modulePath = FindModuleUrl("m3-profile.mjs");
    var start = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
    start.ArgumentList.Add("--input-type=module");
    start.ArgumentList.Add("-e");
    start.ArgumentList.Add($"import {{PROFILE_PATHS}} from {JsonSerializer.Serialize(modulePath)}; console.log(JSON.stringify(PROFILE_PATHS));");
    using var process = Process.Start(start) ?? throw new Exception("Не удалось запустить Node parity check.");
    var output = await process.StandardOutput.ReadToEndAsync();
    var errors = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    Assert(process.ExitCode == 0, "Node parity: " + errors);
    var paths = JsonSerializer.Deserialize<string[]>(output)!;
    Assert(paths.Length == 379 && paths.Distinct(StringComparer.Ordinal).Count() == 379, "JS allowlist must contain 379 unique paths.");
    Assert(paths.Order(StringComparer.Ordinal).SequenceEqual(M3AssetManifestBuilder.ProfilePaths.Order(StringComparer.Ordinal)), "JS/server allowlist расходятся.");
    Throws<NotSupportedException>(() => ((IList<string>)M3AssetManifestBuilder.ProfilePaths)[0] = "modified");
}

static async Task Synthetic()
{
    var root = await CreateFixture();
    try
    {
        var builder = new M3AssetManifestBuilder();
        var result = await builder.BuildAsync(root);
        Assert(result.Files.Count == 379 && result.Visuals.Count == 4, "Unexpected DTO sizes.");
        Assert(result.GameVersion == "4.5.2" && result.ProfileId == "m3-static-452-v11" && result.SchemaVersion == 1 && result.ImporterVersion == 8 && result.MaterialProfileVersion == 4, "DTO versions.");
        Assert(result.Files.Select(file => file.RelativePath).SequenceEqual(result.Files.Select(file => file.RelativePath).Order(StringComparer.Ordinal)), "DTO не sorted.");
        Assert(result.Visuals.Select(visual => visual.VisualId).SequenceEqual(result.Visuals.Select(visual => visual.VisualId).Order(StringComparer.Ordinal)), "Visual IDs не ordinal sorted.");
        Assert(result.Visuals.All(visual => visual.Dependencies.Count == 379 && visual.MaterialProfile == "m3-static-galaxy-v4"), "Visual dependencies.");
        Assert(result.Files.All(file => file.Kind == Path.GetExtension(file.RelativePath)[1..]), "Kind mismatch.");
        Throws<NotSupportedException>(() => ((IList<AssetManifestFile>)result.Files)[0] = result.Files[1]);
        Throws<NotSupportedException>(() => ((IList<AssetManifestVisual>)result.Visuals).Clear());
        Throws<NotSupportedException>(() => ((IList<string>)result.Visuals[0].Dependencies).Clear());
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert(!json.Contains(root, StringComparison.OrdinalIgnoreCase) && !json.Contains("launcher-settings", StringComparison.Ordinal) && Encoding.UTF8.GetByteCount(json) <= 1024 * 1024, "Privacy/metadata size.");
        await CrossLanguageManifest(result);
        var filePath = Path.Combine(root, M3AssetManifestBuilder.ProfilePaths[0]);
        var source = await File.ReadAllBytesAsync(filePath);
        source[0] ^= 1;
        await File.WriteAllBytesAsync(filePath, source);
        Assert((await builder.BuildAsync(root)).ProfileDigest != result.ProfileDigest, "Изменение bytes при той же длине не изменило digest.");
        await File.WriteAllTextAsync(filePath, "other fixture bytes");
        var changed = await builder.BuildAsync(root);
        Assert(changed.ProfileDigest != result.ProfileDigest, "Изменение bytes/длины не изменило digest.");
        Assert(result.Files[0].Sha256 != "", "Предыдущий immutable DTO испорчен.");
    }
    finally
    {
        DeleteFixture(root);
    }
}

static async Task Failures()
{
    var root = await CreateFixture();
    try
    {
        var builder = new M3AssetManifestBuilder();
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await ThrowsAsync<OperationCanceledException>(() => builder.BuildAsync(root, canceled.Token));
        foreach (var version in new[] { "4.5.2", "v4.5.3", "v4.5.2-mod", "v4.5", "" })
        {
            await File.WriteAllTextAsync(Path.Combine(root, "launcher-settings.json"), JsonSerializer.Serialize(new { rawVersion = version }));
            await CheckSanitizedFailure(() => builder.BuildAsync(root), root, "launcher-settings.json");
        }
        await File.WriteAllTextAsync(Path.Combine(root, "launcher-settings.json"), "{\"rawVersion\":\"v4.5.2\",\"rawVersion\":\"v4.5.2\"}");
        await CheckSanitizedFailure(() => builder.BuildAsync(root), root, "launcher-settings.json");
        await File.WriteAllTextAsync(Path.Combine(root, "launcher-settings.json"), "invalid");
        await CheckSanitizedFailure(() => builder.BuildAsync(root), root, "launcher-settings.json");
        await using (var stream = new FileStream(Path.Combine(root, "launcher-settings.json"), FileMode.Create))
        {
            stream.SetLength(1024 * 1024 + 1);
        }
        await CheckSanitizedFailure(() => builder.BuildAsync(root), root, "launcher-settings.json");
        await File.WriteAllTextAsync(Path.Combine(root, "launcher-settings.json"), "{\"rawVersion\":\"v4.5.2\"}");
        var relative = M3AssetManifestBuilder.ProfilePaths[0];
        var path = Path.Combine(root, relative);
        await using (var stream = new FileStream(path, FileMode.Create))
        {
            stream.SetLength(M3AssetManifestBuilder.MAX_FILE_BYTES + 1);
        }
        await CheckSanitizedFailure(() => builder.BuildAsync(root), root, relative);
        await using (var stream = new FileStream(path, FileMode.Create))
        {
            stream.SetLength(M3AssetManifestBuilder.MAX_FILE_BYTES);
        }
        using (var activeCancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(1)))
        {
            await ThrowsAsync<OperationCanceledException>(() => builder.BuildAsync(root, activeCancellation.Token));
        }
        // После отмены read-only handle должен быть освобождён.
        using (var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert(exclusive.CanWrite, "Отмена оставила недоступный файловый handle.");
        }
        File.Delete(path);
        await CheckSanitizedFailure(() => builder.BuildAsync(root), root, relative);
    }
    finally
    {
        DeleteFixture(root);
    }
}

static async Task Reparse()
{
    var root = await CreateFixture();
    var link = Path.Combine(root, "linked-root");
    var source = Path.Combine(root, M3AssetManifestBuilder.ProfilePaths[0]);
    var rootLinkCreated = false;
    var fileLinkCreated = false;
    try
    {
        var target = Path.Combine(root, "outside");
        Directory.CreateDirectory(target);
        try
        {
            Directory.CreateSymbolicLink(link, target);
            rootLinkCreated = true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            throw new CheckSkippedException("Создание symlink недоступно; reparse guard требует отдельной проверки этой среды.");
        }
        await CheckSanitizedFailure(() => new M3AssetManifestBuilder().BuildAsync(link), root, "Корень");
        File.Delete(source);
        File.CreateSymbolicLink(source, Path.Combine(root, "launcher-settings.json"));
        fileLinkCreated = true;
        await CheckSanitizedFailure(() => new M3AssetManifestBuilder().BuildAsync(root), root, "reparse point");
    }
    finally
    {
        if (fileLinkCreated)
        {
            VerifyFixtureLink(root, source, Path.Combine(root, "launcher-settings.json"));
            File.Delete(source);
        }
        if (rootLinkCreated)
        {
            VerifyFixtureLink(root, link, Path.Combine(root, "outside"));
            Directory.Delete(link);
        }
        DeleteFixture(root);
    }
}

static async Task SeamControl()
{
    var root = await CreateFixture();
    try
    {
        var baseline = await new M3AssetManifestBuilder().BuildAsync(root);
        var calls = 0;
        var result = await new M3AssetManifestBuilder(token =>
        {
            Assert(!token.IsCancellationRequested, "Неожиданная отмена control seam.");
            calls++;
            AssertExclusiveHandles(root);
            return Task.CompletedTask;
        }).BuildAsync(root);
        Assert(calls == 1 && result.ProfileDigest == baseline.ProfileDigest && result.Files.Count == 379, "Seam changed an unmodified manifest.");
    }
    finally
    {
        DeleteFixture(root);
    }
}

static async Task SeamMutation()
{
    var root = await CreateFixture();
    try
    {
        var relative = M3AssetManifestBuilder.ProfilePaths[0];
        var path = Path.Combine(root, relative);
        var originalLength = new FileInfo(path).Length;
        var calls = 0;
        M3AssetManifest? published = null;
        var builder = new M3AssetManifestBuilder(async token =>
        {
            calls++;
            var bytes = await File.ReadAllBytesAsync(path, token);
            bytes[0] ^= 1;
            await File.WriteAllBytesAsync(path, bytes, token);
            Assert(new FileInfo(path).Length == originalLength, "Mutation изменила длину fixture.");
        });
        await CheckSanitizedFailure(async () => published = await builder.BuildAsync(root), root, relative);
        Assert(calls == 1 && published is null, "Изменённый источник опубликовал частичный manifest.");
        AssertExclusiveHandles(root);
    }
    finally
    {
        DeleteFixture(root);
    }
}

static async Task SeamCancellation()
{
    var root = await CreateFixture();
    try
    {
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        M3AssetManifest? published = null;
        var builder = new M3AssetManifestBuilder(token =>
        {
            calls++;
            Assert(token == cancellation.Token, "Seam получил посторонний token.");
            AssertExclusiveHandles(root);
            cancellation.Cancel();
            return Task.CompletedTask;
        });
        await ThrowsAsync<OperationCanceledException>(async () => published = await builder.BuildAsync(root, cancellation.Token));
        Assert(calls == 1 && published is null, "Отмена seam опубликовала manifest.");
        AssertExclusiveHandles(root);
    }
    finally
    {
        DeleteFixture(root);
    }
}

static void AssertExclusiveHandles(string root)
{
    foreach (var relative in M3AssetManifestBuilder.ProfilePaths.Append("launcher-settings.json"))
    {
        using var stream = File.Open(Path.Combine(root, relative), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert(stream.CanWrite, "Файл удерживается после завершённого прохода.");
    }
}

static async Task Junctions()
{
    if (!OperatingSystem.IsWindows())
    {
        throw new CheckSkippedException("Directory junction проверяется только на Windows.");
    }
    var root = await CreateFixture();
    var knownLinks = new List<(string Link, string Target)>();
    var script = Path.Combine(root, "junction-helper.ps1");
    try
    {
        await File.WriteAllTextAsync(script, "param([string]$Action,[string]$Link,[string]$Target)\n$ErrorActionPreference = 'Stop'\nif ($Action -eq 'Create') { New-Item -ItemType Junction -Path $Link -Target $Target | Out-Null } elseif ($Action -eq 'Delete') { Remove-Item -LiteralPath $Link -Force } else { throw 'Unknown junction action' }\n");
        var target = Path.Combine(root, "ordinary-target");
        await PopulateFixture(target);
        await PopulateFixture(Path.Combine(target, "child"));
        var sentinel = Path.Combine(target, "sentinel.txt");
        await File.WriteAllTextAsync(sentinel, "synthetic sentinel must survive");
        Assert((await new M3AssetManifestBuilder().BuildAsync(target)).Files.Count == 379, "Regular target failed its positive control.");
        Assert((await new M3AssetManifestBuilder().BuildAsync(Path.Combine(target, "child"))).Files.Count == 379, "Regular ancestor failed its positive control.");
        var rootLink = Path.Combine(root, "junction-root");
        knownLinks.Add((rootLink, target));
        await RunJunction(root, script, "Create", rootLink, target);
        VerifyFixtureLink(root, rootLink, target);
        Assert((File.GetAttributes(rootLink) & FileAttributes.Directory) != 0, "Junction не имеет Directory attribute.");
        await CheckSanitizedFailure(() => new M3AssetManifestBuilder().BuildAsync(rootLink), root, "Корень");
        await CheckSanitizedFailure(() => new M3AssetManifestBuilder().BuildAsync(Path.Combine(rootLink, "child")), root, "Корень");
        await RemoveJunction(root, script, rootLink, target);
        Assert(await File.ReadAllTextAsync(sentinel) == "synthetic sentinel must survive", "Удаление root junction изменило target.");
        var internalLink = Path.Combine(root, "gfx");
        var internalTarget = Path.Combine(root, "gfx-target");
        AssertInsideFixture(root, internalLink);
        AssertInsideFixture(root, internalTarget);
        Directory.Move(internalLink, internalTarget);
        var internalSentinel = Path.Combine(internalTarget, "sentinel.txt");
        await File.WriteAllTextAsync(internalSentinel, "internal target must survive");
        knownLinks.Add((internalLink, internalTarget));
        await RunJunction(root, script, "Create", internalLink, internalTarget);
        VerifyFixtureLink(root, internalLink, internalTarget);
        await CheckSanitizedFailure(() => new M3AssetManifestBuilder().BuildAsync(root), root, "reparse point");
        await RemoveJunction(root, script, internalLink, internalTarget);
        Assert(await File.ReadAllTextAsync(internalSentinel) == "internal target must survive", "Удаление internal junction изменило target.");
    }
    finally
    {
        foreach (var (link, target) in knownLinks)
        {
            await RemoveJunction(root, script, link, target);
        }
        DeleteFixture(root);
    }
}

static void AssertInsideFixture(string root, string path)
{
    var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
    Assert(Path.GetFullPath(path).StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal), "Путь junction вне выделенной fixture.");
}

static void VerifyFixtureLink(string root, string link, string target)
{
    AssertInsideFixture(root, link);
    AssertInsideFixture(root, target);
    Assert((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0, "Ожидался реальный ReparsePoint.");
    FileSystemInfo info = (File.GetAttributes(link) & FileAttributes.Directory) != 0 ? new DirectoryInfo(link) : new FileInfo(link);
    var resolved = info.ResolveLinkTarget(true);
    Assert(info.LinkTarget != null && resolved != null && string.Equals(Path.TrimEndingDirectorySeparator(resolved.FullName), Path.TrimEndingDirectorySeparator(Path.GetFullPath(target)), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal), "Junction/symlink target отличается от ожидаемого.");
}

static async Task RunJunction(string root, string script, string action, string link, string target)
{
    AssertInsideFixture(root, script);
    AssertInsideFixture(root, link);
    AssertInsideFixture(root, target);
    var start = new ProcessStartInfo("pwsh.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script, "-Action", action, "-Link", link, "-Target", target })
    {
        start.ArgumentList.Add(argument);
    }
    Process process;
    try
    {
        process = Process.Start(start) ?? throw new Exception("PowerShell junction helper не запущен.");
    }
    catch (System.ComponentModel.Win32Exception exception) when (action == "Create")
    {
        throw new CheckSkippedException($"PowerShell 7 junction helper недоступен, nativeCode={exception.NativeErrorCode}: {exception.Message}");
    }
    using var processLifetime = process;
    var output = process.StandardOutput.ReadToEndAsync();
    var errors = process.StandardError.ReadToEndAsync();
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    try
    {
        await process.WaitForExitAsync(deadline.Token);
    }
    catch (OperationCanceledException)
    {
        process.Kill();
        await process.WaitForExitAsync();
        throw new Exception("Junction helper превысил 10 секунд.");
    }
    await output;
    var diagnostic = await errors;
    if (process.ExitCode != 0)
    {
        if (action == "Create")
        {
            throw new CheckSkippedException($"Junction недоступен, exit={process.ExitCode}: {diagnostic.Trim()}");
        }
        throw new Exception($"Не удалось безопасно удалить junction, exit={process.ExitCode}: {diagnostic.Trim()}");
    }
}

static async Task RemoveJunction(string root, string script, string link, string target)
{
    AssertInsideFixture(root, link);
    if (!Directory.Exists(link) && !File.Exists(link))
    {
        return;
    }
    VerifyFixtureLink(root, link, target);
    await RunJunction(root, script, "Delete", link, target);
    Assert(!Directory.Exists(link) && !File.Exists(link) && Directory.Exists(target), "Junction остался либо target был удалён.");
}

static async Task Real(string root)
{
    var profile = await new M3AssetManifestBuilder().BuildAsync(root);
    Assert(profile.Files.Count == 379, "Real profile count.");
    foreach (var file in profile.Files)
    {
        await using var source = File.OpenRead(Path.Combine(root, file.RelativePath));
        Assert(source.Length == file.ByteLength && Convert.ToHexString(await SHA256.HashDataAsync(source)).ToLowerInvariant() == file.Sha256, "Реальный hash mismatch.");
    }
    await CrossLanguageManifest(profile);
    Console.WriteLine($"Metadata: files={profile.Files.Count}; digest={profile.ProfileDigest}.");
}

static string FindModuleUrl(string filename)
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "tools", "asset-probe", filename)))
    {
        directory = directory.Parent;
    }
    Assert(directory is not null, "Не найден модуль репозитория: " + filename);
    return new Uri(Path.Combine(directory!.FullName, "tools", "asset-probe", filename)).AbsoluteUri;
}

static async Task CrossLanguageManifest(M3AssetManifest manifest)
{
    // Через stdin передаётся только собственная JSON metadata, не игровые байты/пути установки.
    var modulePath = FindModuleUrl("m3-manifest.mjs");
    var start = new ProcessStartInfo("node")
    {
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        StandardInputEncoding = new UTF8Encoding(false),
        UseShellExecute = false,
        CreateNoWindow = true
    };
    start.ArgumentList.Add("--input-type=module");
    start.ArgumentList.Add("-e");
    start.ArgumentList.Add($"import {{validateM3Manifest}} from {JsonSerializer.Serialize(modulePath)}; let input=''; for await (const chunk of process.stdin) input+=chunk; const result=await validateM3Manifest(JSON.parse(input)); console.log(JSON.stringify({{digest:result.profileDigest,visualIds:result.visuals.map(v=>v.visualId)}}));");
    using var process = Process.Start(start) ?? throw new Exception("Не удалось запустить Node manifest check.");
    var outputTask = process.StandardOutput.ReadToEndAsync();
    var errorTask = process.StandardError.ReadToEndAsync();
    await process.StandardInput.WriteAsync(JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    process.StandardInput.Close();
    await process.WaitForExitAsync();
    var output = await outputTask;
    var errors = await errorTask;
    Assert(process.ExitCode == 0, "JS validateM3Manifest отказал серверной metadata: " + errors);
    using var result = JsonDocument.Parse(output);
    Assert(result.RootElement.GetProperty("digest").GetString() == manifest.ProfileDigest, "C#/JS canonical digest расходятся.");
    Assert(result.RootElement.GetProperty("visualIds").EnumerateArray().Select(value => value.GetString()).SequenceEqual(manifest.Visuals.Select(visual => visual.VisualId)), "C#/JS visual ordering расходится.");
}

static async Task<string> CreateFixture()
{
    var root = Path.Combine(Path.GetTempPath(), "stellaris-asset-check-" + Guid.NewGuid().ToString("N"));
    await PopulateFixture(root);
    return root;
}

static async Task PopulateFixture(string root)
{
    Directory.CreateDirectory(root);
    await File.WriteAllTextAsync(Path.Combine(root, "launcher-settings.json"), "{\"rawVersion\":\"v4.5.2\"}");
    foreach (var path in M3AssetManifestBuilder.ProfilePaths)
    {
        var full = Path.Combine(root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, "synthetic " + path);
    }
}

static void DeleteFixture(string root)
{
    var full = Path.GetFullPath(root);
    var parent = Path.TrimEndingDirectorySeparator(Path.GetTempPath());
    Assert(Path.GetDirectoryName(full) == parent && Path.GetFileName(full).StartsWith("stellaris-asset-check-", StringComparison.Ordinal), "Небезопасная очистка fixture.");
    AssertNoReparse(full);
    Directory.Delete(full, true);
}

static void AssertNoReparse(string directory)
{
    Assert((File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0, "Очистка остановлена: reparse directory.");
    foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
    {
        var attributes = File.GetAttributes(entry);
        Assert((attributes & FileAttributes.ReparsePoint) == 0, "Очистка остановлена: неизвестный reparse point.");
        if ((attributes & FileAttributes.Directory) != 0)
        {
            AssertNoReparse(entry);
        }
    }
}

static async Task CheckSanitizedFailure(Func<Task> run, string root, string expected)
{
    try
    {
        await run();
    }
    catch (AssetManifestException exception)
    {
        Assert(exception.Message.Contains(expected, StringComparison.Ordinal) && !exception.ToString().Contains(root, StringComparison.OrdinalIgnoreCase), "Ошибка раскрыла путь либо потеряла причину.");
        return;
    }
    throw new Exception("Ожидался отказ metadata builder.");
}

static async Task ThrowsAsync<T>(Func<Task> run) where T : Exception
{
    try
    {
        await run();
    }
    catch (T)
    {
        return;
    }
    throw new Exception("Ожидалось исключение " + typeof(T).Name);
}

static void Throws<T>(Action run) where T : Exception
{
    try
    {
        run();
    }
    catch (T)
    {
        return;
    }
    throw new Exception("Ожидалось исключение " + typeof(T).Name);
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new Exception(message);
    }
}

sealed class CheckSkippedException : Exception
{
    public CheckSkippedException(string message) : base(message)
    {
    }
}
