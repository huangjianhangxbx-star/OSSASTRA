/* 脚本：PersistenceTests
 * 功能：隔离经济保存的串行、故障、不可变快照和真实子进程重启验收。
 * 边界：仅使用Candidates/COM01/PersistenceTests/Artifacts内生成的测试档；不接Unity或真实存档。
 */
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Reflection;
using Ossastra.Commerce.Persistence;

static class PersistenceTests
{
    static int passed;
    static string artifactRoot;
    static void Check(bool condition, string name)
    { if (!condition) throw new Exception("FAIL " + name); Console.WriteLine("PASS " + name); passed++; }
    static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
    static string Text(CommerceSaveSnapshot snapshot) => Encoding.UTF8.GetString(snapshot.Payload);
    static string DirectoryFor(string name)
    { var path = Path.Combine(artifactRoot, name); Directory.CreateDirectory(path); return path; }

    // 仅延迟真实磁盘写入，不替代真实文件适配或协调器状态。
    sealed class DelayedStore : ICommerceSaveStore, ICommerceSaveSessionStore
    {
        readonly ICommerceSaveStore inner;
        public readonly TaskCompletionSource<bool> Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<bool> Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls;
        public bool BlockSecond;
        public readonly TaskCompletionSource<bool> SecondEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<bool> SecondRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public DelayedStore(ICommerceSaveStore inner) { this.inner = inner; }
        public void ActivateSession(string profileId, string sessionId)
        { (inner as ICommerceSaveSessionStore)?.ActivateSession(profileId, sessionId); }
        public async Task WriteAsync(CommerceSaveSnapshot snapshot, CancellationToken token)
        {
            int call = Interlocked.Increment(ref calls);
            if (call == 1) { Entered.TrySetResult(true); await Release.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
            if (call == 2 && BlockSecond) { SecondEntered.TrySetResult(true); await SecondRelease.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
            await inner.WriteAsync(snapshot, token);
        }
    }

    static async Task SequenceAndImmutability()
    {
        var disk = new CommerceFileSaveStore(DirectoryFor("sequence"));
        var gate = new DelayedStore(disk) { BlockSecond = true };
        var save = new CommerceSaveCoordinator(gate, "profile-a", "session-a");
        var first = Bytes("coins=17"); save.RequestSave(1, first); first[0] = (byte)'X';
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        save.RequestSave(2, Bytes("coins=12")); save.RequestSave(3, Bytes("coins=9"));
        gate.Release.SetResult(true); await gate.SecondEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Check(Text(disk.Load("profile-a").Snapshot) == "coins=17", "caller mutation cannot change captured bytes");
        var partial = save.Status;
        Check(partial.StateRevision == 3 && partial.PersistedRevision == 1 && !partial.IsSaved, "A completion cannot label dirty B as saved");
        gate.SecondRelease.SetResult(true);
        Check(await save.FlushAsync(), "flush waits through latest queued snapshot");
        var latest = disk.Load("profile-a").Snapshot;
        Check(Text(latest) == "coins=9" && latest.StateRevision == 3 && latest.SaveSequence == 3, "queued latest B wins over older A and intermediate snapshots");
        var copy = latest.Payload; copy[0] = (byte)'X';
        Check(Text(latest) == "coins=9", "snapshot getter cannot mutate saved bytes");
        Check(await save.CloseAsync() && save.Status.IsClosed, "close drains the writer before closing");
        bool rejected = false; try { save.RequestSave(4, Bytes("invalid")); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "closed generation rejects stale UI save requests");
    }

    static async Task ProfileIsolation()
    {
        var disk = new CommerceFileSaveStore(DirectoryFor("profiles"));
        var delayed = new DelayedStore(disk);
        var old = new CommerceSaveCoordinator(delayed, "old-profile", "old-session");
        old.RequestSave(4, Bytes("old-only")); await delayed.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var current = new CommerceSaveCoordinator(disk, "new-profile", "new-session");
        current.RequestSave(1, Bytes("new-only")); Check(await current.FlushAsync(), "new profile writes independently");
        delayed.Release.SetResult(true); Check(await old.FlushAsync(), "old pending write finishes only its own profile");
        Check(Text(disk.Load("new-profile").Snapshot) == "new-only" && Text(disk.Load("old-profile").Snapshot) == "old-only", "late old callback cannot write new profile");
        Check(current.Status.PersistedRevision == 1 && current.Status.SessionId == "new-session", "old completion cannot alter new coordinator status");
        await current.CloseAsync(); await old.CloseAsync();
    }

    static async Task SameProfileGenerationAndIdempotence()
    {
        var disk = new CommerceFileSaveStore(DirectoryFor("same-profile"));
        var delayed = new DelayedStore(disk);
        var old = new CommerceSaveCoordinator(delayed, "profile", "old-generation");
        old.RequestSave(4, Bytes("must-not-return")); await delayed.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var current = new CommerceSaveCoordinator(new CommerceFileSaveStore(DirectoryFor("same-profile")), "profile", "new-generation");
        current.RequestSave(1, Bytes("restored-new-generation")); Check(await current.FlushAsync(), "new same-profile generation commits its own state");
        delayed.Release.SetResult(true);
        Check(!await old.FlushAsync() && Text(disk.Load("profile").Snapshot) == "restored-new-generation", "late old session cannot overwrite reopened same profile");
        var captured = disk.Load("profile").Snapshot;
        await disk.WriteAsync(captured, CancellationToken.None);
        Check(Text(disk.Load("profile").Snapshot) == "restored-new-generation", "unknown-outcome retry of identical saved sequence is idempotent");
        bool rejected = false;
        try { await disk.WriteAsync(new CommerceSaveSnapshot("profile", "new-generation", 1, 1, Bytes("different-content")), CancellationToken.None); }
        catch (InvalidDataException) { rejected = true; }
        Check(rejected && Text(disk.Load("profile").Snapshot) == "restored-new-generation", "same sequence different content cannot replace valid save");
        await current.CloseAsync();
    }

    static async Task RealDiskFailureAndRecovery()
    {
        var disk = new CommerceFileSaveStore(DirectoryFor("disk-failure"));
        var save = new CommerceSaveCoordinator(disk, "test", "one");
        save.RequestSave(1, Bytes("valid-one")); Check(await save.FlushAsync(), "first valid snapshot is committed");
        // 独占主文件制造真实Windows替换失败，上一有效文件仍由真实适配保护。
        using (var locked = new FileStream(disk.PathForProfile("test"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            save.RequestSave(2, Bytes("valid-two"));
            Check(!await save.FlushAsync() && save.Status.HasFailure && !save.Status.CanEdit, "real disk denial blocks only new commerce edits");
            save.MarkDirty(3); save.RequestSave(3, Bytes("valid-three"));
            Check(save.Status.StateRevision == 3 && save.Status.PersistedRevision == 1, "existing domain changes remain dirty during save failure");
        }
        Check(Text(disk.Load("test").Snapshot) == "valid-one", "failed replacement preserves previous valid main file");
        Check(await save.RetryAsync(), "explicit retry persists newest memory state");
        Check(Text(disk.Load("test").Snapshot) == "valid-three" && save.Status.CanEdit, "retry never replays transaction or older failed snapshot");
        Check(Text(disk.LoadBackup("test").Snapshot) == "valid-one", "successful replacement preserves previous valid backup");
        Check(!Directory.GetFiles(DirectoryFor("disk-failure"), "*.tmp").Any(), "success and denial clean owned temporary file");
        await save.CloseAsync();
    }

    static async Task InterruptedTempAndRejectedLoadNeverSeed()
    {
        var disk = new CommerceFileSaveStore(DirectoryFor("interrupted"));
        var save = new CommerceSaveCoordinator(disk, "profile", "session");
        save.RequestSave(1, Bytes("last-valid")); await save.CloseAsync();
        File.WriteAllBytes(disk.PathForProfile("profile") + ".interrupted.tmp", Bytes("partial uncommitted bytes"));
        Check(Text(disk.Load("profile").Snapshot) == "last-valid", "orphan interrupted temporary file is never loaded as current save");
        var path = disk.PathForProfile("profile");
        File.WriteAllBytes(path, Bytes("corrupt-user-evidence"));
        var restart = new CommerceSaveCoordinator(disk, "profile", "after-restart");
        restart.RequestSave(2, Bytes("must-not-seed-over-corruption"));
        Check(!await restart.FlushAsync() && File.ReadAllText(path) == "corrupt-user-evidence", "invalid existing main refuses save instead of replacing evidence with seed data");
        Check(!restart.Status.CanEdit, "invalid existing main leaves explicit recovery state");
        var gap = new CommerceSaveCoordinator(new CommerceFileSaveStore(DirectoryFor("dirty-gap")), "profile", "session");
        gap.MarkDirty(2);
        Check(!await gap.FlushAsync() && !await gap.CloseAsync() && !gap.Status.IsClosed, "flush cannot label uncaptured newer domain revision as saved or closed");
    }

    static async Task LoadValidationAndExplicitBackup()
    {
        var disk = new CommerceFileSaveStore(DirectoryFor("validation"));
        Check(disk.Load("missing").Status == CommerceSaveLoadStatus.Missing, "missing profile is distinguishable from invalid profile");
        var absentDirectory = Path.Combine(DirectoryFor("validation"), "not-created");
        Check(new CommerceFileSaveStore(absentDirectory).RestoreBackup("missing").Status == CommerceSaveLoadStatus.Missing && !Directory.Exists(absentDirectory),
            "recovering an absent backup reports missing without creating a save directory");
        var save = new CommerceSaveCoordinator(disk, "test", "one");
        save.RequestSave(1, Bytes("known-definition"), 1, "catalog-a"); await save.FlushAsync();
        save.RequestSave(2, Bytes("known-definition-v2"), 1, "catalog-a"); await save.FlushAsync();
        var path = disk.PathForProfile("test"); var valid = File.ReadAllBytes(path);
        Check(disk.Load("test", s => s.DefinitionFingerprint == "catalog-b" ? null : "missing definition").Status == CommerceSaveLoadStatus.Invalid,
            "domain validator rejects missing definitions without seeding");
        Check(disk.Load("test", s => s.PayloadSchema == 2 ? null : "unknown schema").Status == CommerceSaveLoadStatus.Invalid,
            "unknown payload schema is rejected before domain activation");
        var broken = (byte[])valid.Clone(); broken[broken.Length - 1] ^= 127; File.WriteAllBytes(path, broken);
        Check(disk.Load("test").Status == CommerceSaveLoadStatus.Invalid && File.ReadAllBytes(path).SequenceEqual(broken), "corrupt snapshot is rejected and preserved unchanged");
        Check(Text(disk.LoadBackup("test").Snapshot) == "known-definition", "backup can be inspected without silently replacing corrupt main");
        Check(disk.RestoreBackup("test", s => s.PayloadSchema == 1 ? null : "bad schema").Status == CommerceSaveLoadStatus.Loaded,
            "explicit backup recovery restores a validated version");
        Check(Text(disk.Load("test").Snapshot) == "known-definition" && Directory.GetFiles(DirectoryFor("validation"), "*.corrupt-*").Length == 1,
            "explicit recovery retains corrupt evidence and recovered version");
        await save.CloseAsync();
    }

    static async Task BackupRecoveryRejectsLateWriter()
    {
        var disk = new CommerceFileSaveStore(DirectoryFor("backup-generation"));
        var initial = new CommerceSaveCoordinator(disk, "profile", "before-recovery");
        initial.RequestSave(1, Bytes("backup-version")); await initial.FlushAsync();
        initial.RequestSave(2, Bytes("main-version")); await initial.CloseAsync();
        var delayed = new DelayedStore(disk);
        var old = new CommerceSaveCoordinator(delayed, "profile", "writer-started-before-recovery", 2, 2);
        old.RequestSave(3, Bytes("late-memory-must-not-replace-recovery"));
        await delayed.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var restored = disk.RestoreBackup("profile");
        delayed.Release.SetResult(true);
        Check(restored.Status == CommerceSaveLoadStatus.Loaded && !await old.FlushAsync() && Text(disk.Load("profile").Snapshot) == "backup-version",
            "backup recovery fences an already queued old writer instead of letting it overwrite recovered bytes");
    }

    static async Task BackupRecoveryRejectsStalePreview()
    {
        var folder = DirectoryFor("backup-preview");
        var disk = new CommerceFileSaveStore(folder);
        var save = new CommerceSaveCoordinator(disk, "profile", "preview-session");
        save.RequestSave(1, Bytes("version-one")); await save.FlushAsync();
        save.RequestSave(2, Bytes("version-two")); await save.FlushAsync();
        var preview = disk.LoadBackup("profile").Snapshot;
        save.RequestSave(3, Bytes("version-three")); await save.CloseAsync();
        var main = File.ReadAllBytes(disk.PathForProfile("profile"));
        var backup = File.ReadAllBytes(disk.BackupPathForProfile("profile"));
        var marker = File.ReadAllText(disk.PathForProfile("profile") + ".session");
        var rejected = disk.RestoreBackup("profile", preview, null);
        Check(rejected.Status == CommerceSaveLoadStatus.Invalid && File.ReadAllBytes(disk.PathForProfile("profile")).SequenceEqual(main) &&
            File.ReadAllBytes(disk.BackupPathForProfile("profile")).SequenceEqual(backup) &&
            File.ReadAllText(disk.PathForProfile("profile") + ".session") == marker && !Directory.GetFiles(folder, "*.corrupt-*").Any(),
            "expired backup preview is rejected without altering main backup marker or evidence");
        var current = disk.LoadBackup("profile").Snapshot;
        var forged = new CommerceSaveSnapshot(current.ProfileId, current.SessionId, current.StateRevision, current.SaveSequence,
            Bytes("different-bytes-same-version"), current.PayloadSchema, current.DefinitionFingerprint);
        Check(disk.RestoreBackup("profile", forged, null).Status == CommerceSaveLoadStatus.Invalid,
            "preview comparison includes frozen bytes rather than trusting matching revision and sequence");
        var restored = disk.RestoreBackup("profile", current, null);
        Check(restored.Status == CommerceSaveLoadStatus.Loaded && Text(restored.Snapshot) == "version-two" &&
            File.ReadAllText(disk.PathForProfile("profile") + ".session") != marker && Directory.GetFiles(folder, "*.corrupt-*").Length == 1,
            "confirmed current preview restores exact backup creates a new fence and retains previous main evidence");
    }

    static async Task RealProcessRestart()
    {
        var path = DirectoryFor("process-restart");
        var writer = await Child("child-write", path); var reader = await Child("child-read", path);
        Check(writer.code == 0 && reader.code == 0 && writer.pid != reader.pid, "different terminated processes write and restore the same economy bytes");
        Check(reader.output.Contains("RESTORED coins=23;stock=4;ground=item-a;hands=weapon-7;clock=19"), "restart reads persisted economy payload without initial grants");
        Console.WriteLine("PROCESS_EVIDENCE writer=" + writer.pid + " reader=" + reader.pid);
    }
    static async Task<(int code, int pid, string output)> Child(string mode, string path)
    {
        var info = new ProcessStartInfo(Environment.ProcessPath) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        info.ArgumentList.Add(mode); info.ArgumentList.Add(path);
        using var process = Process.Start(info); int pid = process.Id;
        string output = await process.StandardOutput.ReadToEndAsync(); string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(); Console.Write(output); if (error.Length > 0) Console.Write(error);
        return (process.ExitCode, pid, output);
    }
    static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length == 2 && args[0] == "child-write")
            {
                var coordinator = new CommerceSaveCoordinator(new CommerceFileSaveStore(args[1]), "restart", "process-writer");
                coordinator.RequestSave(7, Bytes("coins=23;stock=4;ground=item-a;hands=weapon-7;clock=19"));
                return await coordinator.CloseAsync() ? 0 : 2;
            }
            if (args.Length == 2 && args[0] == "child-read")
            {
                var loaded = new CommerceFileSaveStore(args[1]).Load("restart");
                if (loaded.Status != CommerceSaveLoadStatus.Loaded || loaded.Snapshot.StateRevision != 7) return 3;
                Console.WriteLine("RESTORED " + Text(loaded.Snapshot)); return 0;
            }
            artifactRoot = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Artifacts", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
            artifactRoot = Path.GetFullPath(artifactRoot); Directory.CreateDirectory(artifactRoot);
            Console.WriteLine("ISOLATED_ROOT " + artifactRoot);
            await SequenceAndImmutability(); await ProfileIsolation(); await SameProfileGenerationAndIdempotence(); await RealDiskFailureAndRecovery();
            await LoadValidationAndExplicitBackup(); await InterruptedTempAndRejectedLoadNeverSeed(); await RealProcessRestart();
            await BackupRecoveryRejectsLateWriter(); await BackupRecoveryRejectsStalePreview();
            Console.WriteLine("TOTAL " + passed); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
