/* 脚本：CommerceSaveCoordinator
 * 功能：单档案/会话的唯一串行保存；合并尚未开始的快照，分离领域修订、保存序号与落盘修订。
 * 边界：后台只写不可变字节，不回滚成交、不重放交易、不捕获角色或冻结既有消费。
 * 协作：宿主主线程MarkDirty/Capture/RequestSave；写失败只限制新的Commerce编辑，显式Retry保存最新状态。
 */
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ossastra.Commerce.Persistence
{
    public sealed class CommerceSaveCoordinator
    {
        private readonly object gate = new object();
        private readonly ICommerceSaveStore store;
        private readonly string profileId, sessionId;
        private CommerceSaveSnapshot pending;
        private Task worker = Task.CompletedTask;
        private long stateRevision, saveSequence, persistedRevision;
        private bool running, failed, closing, closed;
        private string lastError;

        public CommerceSaveCoordinator(ICommerceSaveStore store, string profileId, string sessionId,
            long persistedRevision = 0, long initialSaveSequence = 0)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            CommerceSaveSnapshot.ValidateIdentity(profileId, nameof(profileId));
            CommerceSaveSnapshot.ValidateIdentity(sessionId, nameof(sessionId));
            if (persistedRevision < 0 || initialSaveSequence < 0) throw new ArgumentException("Invalid restored save counters");
            this.store = store; this.profileId = profileId; this.sessionId = sessionId;
            stateRevision = this.persistedRevision = persistedRevision; saveSequence = initialSaveSequence;
            // 重新打开同一档案也是新代际；旧协调器即使稍后完成异步准备，也不得替换新会话文件。
            var sessions = store as ICommerceSaveSessionStore;
            if (sessions != null) sessions.ActivateSession(profileId, sessionId);
        }

        public CommerceSaveStatus Status
        {
            get
            {
                lock (gate) return new CommerceSaveStatus
                {
                    ProfileId = profileId, SessionId = sessionId, StateRevision = stateRevision,
                    SaveSequence = saveSequence, PersistedRevision = persistedRevision,
                    IsSaving = running || pending != null && !failed, HasFailure = failed,
                    IsClosing = closing, IsClosed = closed, LastError = lastError
                };
            }
        }

        /// <summary>即使写盘失败，已受理的能力消费或全灭仍须留下新脏版本；不由这里拒绝领域事实。</summary>
        public void MarkDirty(long revision)
        {
            lock (gate)
            {
                if (closed) throw new InvalidOperationException("Commerce save session is closed");
                if (revision < stateRevision) throw new ArgumentException("Commerce state revision cannot move backwards");
                stateRevision = revision;
            }
        }

        /// <summary>调用方必须先在主线程完成领域快照/编码；方法返回后原byte[]可安全复用。</summary>
        public long RequestSave(long revision, byte[] payload, int payloadSchema = 1, string definitionFingerprint = "")
        {
            lock (gate)
            {
                if (closed || closing) throw new InvalidOperationException("Commerce save session is closing/closed");
                if (revision < stateRevision) throw new ArgumentException("Stale commerce snapshot");
                long next = checked(saveSequence + 1);
                var captured = new CommerceSaveSnapshot(profileId, sessionId, revision, next, payload, payloadSchema, definitionFingerprint);
                stateRevision = revision; saveSequence = next; pending = captured;
                // 首次写失败后不自动反复重试；新领域事实仍替换待写快照，显式重试时只落最新状态。
                if (!failed) StartWorker();
                return next;
            }
        }

        private void StartWorker()
        {
            if (running || pending == null || closed) return;
            running = true; worker = Task.Run(DrainAsync);
        }

        private async Task DrainAsync()
        {
            while (true)
            {
                CommerceSaveSnapshot snapshot;
                lock (gate)
                {
                    if (pending == null) { running = false; return; }
                    snapshot = pending; pending = null;
                }
                try
                {
                    await store.WriteAsync(snapshot, CancellationToken.None).ConfigureAwait(false);
                    lock (gate)
                    {
                        persistedRevision = Math.Max(persistedRevision, snapshot.StateRevision);
                        lastError = null;
                    }
                }
                catch (Exception error)
                {
                    lock (gate)
                    {
                        // B已排队时留下B；没有B才保留A。不重放经济命令，只重试同一最新快照。
                        if (pending == null || pending.SaveSequence < snapshot.SaveSequence) pending = snapshot;
                        failed = true; running = false; lastError = error.GetType().Name + ": " + error.Message;
                    }
                    return;
                }
            }
        }

        public async Task<bool> FlushAsync()
        {
            while (true)
            {
                Task current;
                lock (gate)
                {
                    if (failed) return false;
                    if (!running && pending == null) return persistedRevision >= stateRevision;
                    StartWorker(); current = worker;
                }
                await current.ConfigureAwait(false);
            }
        }

        public Task<bool> RetryAsync()
        {
            lock (gate)
            {
                if (closed) return Task.FromResult(false);
                failed = false; lastError = null; StartWorker();
            }
            return FlushAsync();
        }

        /// <summary>换档/退出必须等待此结果。失败不伪装关闭，宿主保留内存并显示重试/退出风险。</summary>
        public async Task<bool> CloseAsync()
        {
            lock (gate) { if (closed) return true; closing = true; }
            bool success = await FlushAsync().ConfigureAwait(false);
            lock (gate) { closing = false; if (success) closed = true; }
            return success;
        }
    }
}
