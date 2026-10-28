/* 脚本：CommerceSaveContracts
 * 功能：经济保存的不透明不可变字节快照、状态及文件端口。
 * 边界：不认识Unity、活Ledger、角色或领域DTO；领域校验在启用恢复状态之前完成。
 * 协作：主线程采集并编码领域快照，协调器冻结字节后交给后台文件适配。
 */
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ossastra.Commerce.Persistence
{
    public sealed class CommerceSaveSnapshot
    {
        private readonly byte[] payload;
        public const int MaximumPayloadBytes = 64 * 1024 * 1024;
        public string ProfileId { get; }
        public string SessionId { get; }
        public long StateRevision { get; }
        public long SaveSequence { get; }
        public int PayloadSchema { get; }
        public string DefinitionFingerprint { get; }
        public byte[] Payload => (byte[])payload.Clone();

        public CommerceSaveSnapshot(string profileId, string sessionId, long stateRevision, long saveSequence,
            byte[] payload, int payloadSchema = 1, string definitionFingerprint = "")
        {
            ValidateIdentity(profileId, nameof(profileId));
            ValidateIdentity(sessionId, nameof(sessionId));
            if (stateRevision < 0 || saveSequence < 1 || payloadSchema < 1 || payload == null || payload.Length > MaximumPayloadBytes ||
                definitionFingerprint != null && definitionFingerprint.Length > 4096)
                throw new ArgumentException("Invalid commerce save revision/schema/payload");
            ProfileId = profileId; SessionId = sessionId; StateRevision = stateRevision; SaveSequence = saveSequence;
            PayloadSchema = payloadSchema; DefinitionFingerprint = definitionFingerprint ?? "";
            this.payload = (byte[])payload.Clone();
        }

        internal static void ValidateIdentity(string value, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 256)
                throw new ArgumentException("Missing/oversized commerce identity", parameter);
        }
    }

    public enum CommerceSaveLoadStatus { Missing, Loaded, Invalid }

    public sealed class CommerceSaveLoadResult
    {
        public CommerceSaveLoadStatus Status { get; }
        public CommerceSaveSnapshot Snapshot { get; }
        public string Error { get; }
        public string Path { get; }
        public bool BackupAvailable { get; }
        internal CommerceSaveLoadResult(CommerceSaveLoadStatus status, CommerceSaveSnapshot snapshot,
            string error, string path, bool backupAvailable)
        { Status = status; Snapshot = snapshot; Error = error; Path = path; BackupAvailable = backupAvailable; }
    }

    /// <summary>只接收已冻结字节。实现不得读取Unity或通过回调重新采集当前领域状态。</summary>
    public interface ICommerceSaveStore
    {
        Task WriteAsync(CommerceSaveSnapshot snapshot, CancellationToken cancellationToken);
    }

    /// <summary>打开新档案代际时使旧写入失效；装饰适配应转发此端口。</summary>
    public interface ICommerceSaveSessionStore
    {
        void ActivateSession(string profileId, string sessionId);
    }

    /// <summary>这是协调器的值快照；UI轮询，不从后台回调访问Unity对象。</summary>
    public sealed class CommerceSaveStatus
    {
        public string ProfileId { get; internal set; }
        public string SessionId { get; internal set; }
        public long StateRevision { get; internal set; }
        public long SaveSequence { get; internal set; }
        public long PersistedRevision { get; internal set; }
        public bool IsSaving { get; internal set; }
        public bool HasFailure { get; internal set; }
        public bool IsClosed { get; internal set; }
        public bool IsClosing { get; internal set; }
        public string LastError { get; internal set; }
        public bool CanEdit => !HasFailure && !IsClosed && !IsClosing;
        public bool IsSaved => !HasFailure && !IsSaving && PersistedRevision >= StateRevision;
    }
}
