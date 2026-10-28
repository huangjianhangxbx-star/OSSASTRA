/* 脚本：CommerceFileSaveStore
 * 功能：带完整校验的经济快照文件；同目录临时文件刷盘后原子替换，保留上一有效备份。
 * 边界：只操作显式目录中的档案文件；不自动播种、不自动选择备份、不访问Unity或活领域对象。
 * 协作：外层先校验schema/定义/重复ID，再启用候选经济状态；坏档保留，恢复备份必须显式调用。
 */
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Ossastra.Commerce.Persistence
{
    public sealed class CommerceFileSaveStore : ICommerceSaveStore, ICommerceSaveSessionStore
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("OCS1");
        private readonly string directory;
        private const int EnvelopeSchema = 1;

        public CommerceFileSaveStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Missing commerce save directory");
            this.directory = Path.GetFullPath(directory);
        }

        public string PathForProfile(string profileId)
        {
            CommerceSaveSnapshot.ValidateIdentity(profileId, nameof(profileId));
            // 档案名不参与路径拼接，防止名称中的../或分隔符越过宿主指定目录。
            using (var hash = SHA256.Create())
            {
                string key = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(profileId))).Replace("-", "").ToLowerInvariant();
                return Path.Combine(directory, "economy-" + key + ".comsave");
            }
        }
        public string BackupPathForProfile(string profileId) => PathForProfile(profileId) + ".bak";

        public void ActivateSession(string profileId, string sessionId)
        {
            CommerceSaveSnapshot.ValidateIdentity(sessionId, nameof(sessionId));
            Directory.CreateDirectory(directory);
            string path = PathForProfile(profileId), marker = path + ".session";
            using (var lease = new FileStream(path + ".write.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                var bytes = Encoding.UTF8.GetBytes(sessionId);
                using (var stream = new FileStream(marker, FileMode.Create, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            }
        }

        public CommerceSaveLoadResult Load(string profileId, Func<CommerceSaveSnapshot, string> validator = null)
            => Read(PathForProfile(profileId), profileId, validator);
        public CommerceSaveLoadResult LoadBackup(string profileId, Func<CommerceSaveSnapshot, string> validator = null)
            => Read(BackupPathForProfile(profileId), profileId, validator);

        private CommerceSaveLoadResult Read(string path, string profileId, Func<CommerceSaveSnapshot, string> validator)
        {
            bool backup = File.Exists(BackupPathForProfile(profileId));
            if (!File.Exists(path)) return new CommerceSaveLoadResult(CommerceSaveLoadStatus.Missing, null, null, path, backup);
            try
            {
                var snapshot = Decode(ReadBounded(path));
                if (snapshot.ProfileId != profileId) throw new InvalidDataException("Commerce profile identity does not match file");
                var denied = validator == null ? null : validator(snapshot);
                if (denied != null) throw new InvalidDataException(denied);
                return new CommerceSaveLoadResult(CommerceSaveLoadStatus.Loaded, snapshot, null, path, backup);
            }
            catch (Exception error)
            { return new CommerceSaveLoadResult(CommerceSaveLoadStatus.Invalid, null, error.GetType().Name + ": " + error.Message, path, backup); }
        }

        public Task WriteAsync(CommerceSaveSnapshot snapshot, CancellationToken cancellationToken)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            return Task.Run(() => Write(snapshot, cancellationToken), cancellationToken);
        }

        private void Write(CommerceSaveSnapshot snapshot, CancellationToken token)
        {
            Directory.CreateDirectory(directory);
            string path = PathForProfile(snapshot.ProfileId), temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            // 文件锁也覆盖多个进程/意外重复安装；只允许一个实际替换者，不静默争抢同档案。
            using (var lease = new FileStream(path + ".write.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    string marker = path + ".session";
                    if (!File.Exists(marker) || new FileInfo(marker).Length > 4096 || File.ReadAllText(marker, Encoding.UTF8) != snapshot.SessionId)
                        throw new InvalidDataException("Commerce save session has been superseded");
                    if (File.Exists(path))
                    {
                        var previous = Decode(ReadBounded(path));
                        if (previous.ProfileId != snapshot.ProfileId) throw new InvalidDataException("Existing commerce profile mismatch");
                        if (previous.SessionId == snapshot.SessionId && previous.SaveSequence >= snapshot.SaveSequence)
                        {
                            // 替换已经成功但调用方未获确认时，同一完整快照重试是成功；不同内容绝不能复用序号。
                            if (previous.SaveSequence == snapshot.SaveSequence && Encode(previous).SequenceEqual(Encode(snapshot))) return;
                            throw new InvalidDataException("Stale or conflicting commerce save sequence");
                        }
                    }
                    var bytes = Encode(snapshot);
                    using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                    // 先读回校验完整临时文件，坏档永不挤掉上一份有效文件或备份。
                    Decode(ReadBounded(temp)); token.ThrowIfCancellationRequested();
                    if (File.Exists(path)) File.Replace(temp, path, BackupPathForProfile(snapshot.ProfileId));
                    else File.Move(temp, path);
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
        }

        /// <summary>仅在用户/宿主明确选择恢复备份后调用；先验证备份，并保留损坏主档证据。</summary>
        public CommerceSaveLoadResult RestoreBackup(string profileId, Func<CommerceSaveSnapshot, string> validator = null)
            => RestoreBackupCore(profileId, null, validator);

        /// <summary>显式确认预览后使用；同一写锁内重验完整备份，过期预览不能恢复另一版本。</summary>
        public CommerceSaveLoadResult RestoreBackup(string profileId, CommerceSaveSnapshot preview, Func<CommerceSaveSnapshot, string> validator)
        {
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            return RestoreBackupCore(profileId, preview, validator);
        }

        private CommerceSaveLoadResult RestoreBackupCore(string profileId, CommerceSaveSnapshot preview, Func<CommerceSaveSnapshot, string> validator)
        {
            string path = PathForProfile(profileId), temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            if (!Directory.Exists(directory)) return LoadBackup(profileId, validator);
            try
            {
                // 检查和提交共用写锁，避免确认后另一次写入改变备份来源。
                using (var lease = new FileStream(path + ".write.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    var backup = LoadBackup(profileId, validator);
                    if (backup.Status != CommerceSaveLoadStatus.Loaded) return backup;
                    if (preview != null && !Encode(preview).SequenceEqual(Encode(backup.Snapshot)))
                        return new CommerceSaveLoadResult(CommerceSaveLoadStatus.Invalid, null, "备份已变化，请重新检查并确认恢复版本", backup.Path, true);
                    try
                    {
                        var bytes = Encode(backup.Snapshot);
                        using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                        Decode(ReadBounded(temp));
                        if (File.Exists(path))
                            File.Copy(path, path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N"));
                        // 恢复也是新代际：同锁内撤销退出Play前仍排队的旧writer，不能让它随后盖回恢复结果。
                        var marker = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString("N"));
                        using (var stream = new FileStream(path + ".session", FileMode.Create, FileAccess.Write, FileShare.None))
                        { stream.Write(marker, 0, marker.Length); stream.Flush(true); }
                        if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
                    }
                    finally { if (File.Exists(temp)) File.Delete(temp); }
                    return Load(profileId, validator);
                }
            }
            catch (Exception error)
            { return new CommerceSaveLoadResult(CommerceSaveLoadStatus.Invalid, null, error.GetType().Name + ": " + error.Message, path, true); }
        }

        private static byte[] Encode(CommerceSaveSnapshot snapshot)
        {
            byte[] body;
            using (var buffer = new MemoryStream())
            {
                using (var writer = new BinaryWriter(buffer, Encoding.UTF8, true))
                {
                    writer.Write(EnvelopeSchema); writer.Write(snapshot.ProfileId); writer.Write(snapshot.SessionId);
                    writer.Write(snapshot.StateRevision); writer.Write(snapshot.SaveSequence); writer.Write(snapshot.PayloadSchema);
                    writer.Write(snapshot.DefinitionFingerprint); var payload = snapshot.Payload; writer.Write(payload.Length); writer.Write(payload);
                }
                body = buffer.ToArray();
            }
            using (var buffer = new MemoryStream())
            using (var writer = new BinaryWriter(buffer))
            using (var hash = SHA256.Create())
            { writer.Write(Magic); writer.Write(body.Length); writer.Write(body); writer.Write(hash.ComputeHash(body)); return buffer.ToArray(); }
        }

        private static CommerceSaveSnapshot Decode(byte[] bytes)
        {
            if (bytes.Length < 40 || bytes.Length > CommerceSaveSnapshot.MaximumPayloadBytes + 65536)
                throw new InvalidDataException("Commerce save length is invalid");
            using (var buffer = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(buffer, Encoding.UTF8, true))
            {
                if (!reader.ReadBytes(4).SequenceEqual(Magic)) throw new InvalidDataException("Unknown commerce save header");
                int length = reader.ReadInt32();
                if (length < 1 || length != bytes.Length - 40) throw new InvalidDataException("Incomplete commerce save");
                var body = reader.ReadBytes(length); var checksum = reader.ReadBytes(32);
                using (var hash = SHA256.Create())
                    if (!hash.ComputeHash(body).SequenceEqual(checksum)) throw new InvalidDataException("Commerce save checksum mismatch");
                using (var payloadStream = new MemoryStream(body, false))
                using (var fields = new BinaryReader(payloadStream, Encoding.UTF8, true))
                {
                    if (fields.ReadInt32() != EnvelopeSchema) throw new InvalidDataException("Unknown commerce envelope schema");
                    string profile = fields.ReadString(), session = fields.ReadString();
                    long revision = fields.ReadInt64(), sequence = fields.ReadInt64(); int schema = fields.ReadInt32();
                    string fingerprint = fields.ReadString(); int payloadLength = fields.ReadInt32();
                    if (payloadLength < 0 || payloadLength > CommerceSaveSnapshot.MaximumPayloadBytes || payloadLength != payloadStream.Length - payloadStream.Position)
                        throw new InvalidDataException("Commerce payload length mismatch");
                    return new CommerceSaveSnapshot(profile, session, revision, sequence, fields.ReadBytes(payloadLength), schema, fingerprint);
                }
            }
        }

        private static byte[] ReadBounded(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length < 40 || stream.Length > CommerceSaveSnapshot.MaximumPayloadBytes + 65536)
                    throw new InvalidDataException("Commerce save file exceeds length limit");
                var bytes = new byte[(int)stream.Length]; int offset = 0;
                while (offset < bytes.Length)
                { int count = stream.Read(bytes, offset, bytes.Length - offset); if (count == 0) throw new EndOfStreamException("Incomplete commerce file"); offset += count; }
                if (stream.ReadByte() != -1) throw new InvalidDataException("Commerce file changed while reading");
                return bytes;
            }
        }
    }
}
