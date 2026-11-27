/*
 * 脚本：ChainDaggerExecutorSO
 * 功能：从真实释放事件投出单目标链匕首，接收正式命中并等待目标经自身运动器到位。
 * 边界：配置只保存作者数值。Run 持有本次链、目标和位移句柄，连段窗口与第四段准入由宿主管理。
 * 协作：共享投递器报告首个实物接触，ActionImpactMotor 决定牵引是否合法并提交真实位移。
 */
using System;
using Ossastra.CharacterCore;
using Ossastra.CharacterCore.Abilities;
using Ossastra.Combat;
using Spine.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ossastra.Characters
{
    [CreateAssetMenu(menuName = "OSSASTRA/Abilities/Executors/Chain Dagger")]
    public sealed class ChainDaggerExecutorSO : AbilityExecutorSO
    {
        public DeliveryParameters projectile = new DeliveryParameters
        {
            mode = DeliveryMode.Projectile, range = 5, speed = 16, collisionRadius = .16f
        };
        [Min(.01f)] public float pullSpeed = 15;
        [Min(.01f)] public float pullTimeout = .50f;
        [Min(0)] public float dockDistance = .86f;
        [Min(.01f)] public float dockTolerance = .12f;
        [Min(.01f)] public float heightTolerance = .65f;
        [Min(.01f)] public float maxOwnerDrift = .75f;

        public override bool UsesBodyAction => true;

        public override void Validate(AbilityDefinitionSO definition)
            => throw new ArgumentException("请在普通攻击的专用行为栏配置链匕首，以绑定本段动画释放与连段资格。");

        public override CompiledAbilityExecutor CompileExecutor()
        {
            ValidateParameters();
            var delivery = projectile.Freeze();
            delivery.mode = DeliveryMode.Projectile;
            delivery.activeDuration = 0;
            delivery.followSource = false;
            delivery.stopAtFirstContact = true;
            delivery.horizontalSideView = true;
            return new Executor(delivery, pullSpeed, pullTimeout, dockDistance, dockTolerance, heightTolerance, maxOwnerDrift);
        }

        private void ValidateParameters()
        {
            if (projectile == null || projectile.collisionLayers.value == 0 ||
                !Positive(pullSpeed) || !Positive(pullTimeout) || !Positive(dockTolerance) ||
                !Positive(heightTolerance) || !Positive(maxOwnerDrift) || !float.IsFinite(dockDistance) || dockDistance < 0)
                throw new ArgumentException("链匕首需要可用的投递遮罩、回拉速度、回拉期限和停靠参数。");
        }

        private static bool Positive(float value) => value > 0 && float.IsFinite(value);

        private sealed class Executor : CompiledAbilityExecutor
        {
            public override bool UsesBodyAction => true;
            public override bool SupportsBasicAction => true;
            public override float AimRange => delivery.range;

            private readonly DeliveryParameters delivery;
            public readonly float pullSpeed, pullTimeout, dockDistance, dockTolerance, heightTolerance, maxOwnerDrift;

            public Executor(DeliveryParameters delivery, float pullSpeed, float pullTimeout,
                float dockDistance, float dockTolerance, float heightTolerance, float maxOwnerDrift)
            {
                this.delivery = delivery;
                this.pullSpeed = pullSpeed;
                this.pullTimeout = pullTimeout;
                this.dockDistance = dockDistance;
                this.dockTolerance = dockTolerance;
                this.heightTolerance = heightTolerance;
                this.maxOwnerDrift = maxOwnerDrift;
            }

            public override CompiledAbilityExecutor Prepare(CharacterRoot owner, IAbilitySource source,
                CharacterAnimationPreparation preparation, ActionDefinitionSO initialAction)
            {
                if (owner.StateMovement?.IsSideView != true)
                    throw new ArgumentException("链匕首需要横版运动平面。");
                return this;
            }

            public override bool CanUseSource(IAbilitySource source) => source != null && source.IsAvailable;
            public override AbilityRun CreateRun(AbilityRunContext context) => new Run(context, delivery, this);
        }

        private sealed class Run : AbilityRun
        {
            private enum Phase { WaitingForRelease, Flying, Pulling }
            private const string Channel = "chain-dagger";
            private readonly DeliveryParameters delivery;
            private readonly Executor parameters;
            private Phase phase;
            private double deadline;
            private int emissionIndex, targetLife;
            private Guid releaseId;
            private float facing;
            private CombatVitals target;
            private ActionImpactMotor.TargetMotion motion;
            private Vector3 pullFrom, targetAttachment;
            private ChainView view;

            public Run(AbilityRunContext context, DeliveryParameters delivery, Executor parameters) : base(context)
            {
                this.delivery = delivery;
                this.parameters = parameters;
            }

            public override void OnActionFact(ActionFact fact)
            {
                if (Ended || fact.ActionId != Context.id || phase != Phase.WaitingForRelease) return;
                if (fact.Kind == ActionFactKind.Finished)
                {
                    End(false, "动画已结束，未收到释放事件");
                    return;
                }
                if (fact.Kind != ActionFactKind.Released) return;

                var release = Context.FrozenBasicRelease;
                if (release == null)
                {
                    End(false, "当前动作没有有效的伤害释放");
                    return;
                }
                var direction = Context.BasicReleaseDirection;
                if (Mathf.Abs(direction.x) < .001f)
                {
                    End(false, "没有可用的横向投掷方向");
                    return;
                }

                int damage = release.Damage;
                float posture = (float)release.PostureImpact;
                emissionIndex = release.EmissionIndex;
                releaseId = release.ActionId;
                facing = Mathf.Sign(direction.x);
                phase = Phase.Flying;
                // 位移仍由投递器推进。较长安全期限只回收失去回执的飞行，不会延长动画的接段期限。
                deadline = Context.owner.SimulationTime + delivery.range / delivery.speed + .15f;
                view = ChainView.Create(Context.owner, facing);
                Context.ReportBehavior("匕首飞行");
                if (!Context.TryDeliver(new AbilityEmission(0, damage, emissionIndex), delivery, Channel, posture, ObserveFlight))
                    End(false, "来源或投递拒绝释放");
            }

            public override void OnContact(CombatContactBatch batch)
            {
                if (Ended || phase != Phase.Flying || batch.ParentExecutionId != Context.id ||
                    batch.Source != Context.owner.Vitals || batch.Cause != CombatContactCause.Direct ||
                    batch.EmissionIndex != emissionIndex || batch.Channel != Channel || batch.ActionId != releaseId) return;
                if (!Context.CanContinueBehavior)
                {
                    End(false, "行为身份或接段期限已失效");
                    return;
                }
                if (batch.Receipts.Count == 0)
                {
                    string reason = batch.EnvironmentContact.HasValue ? "匕首被地形阻挡" :
                        batch.EffectSemantic == CombatDeliveryQueue.RejectedProjectileContact ? "首个接触目标不合法" : "匕首落空";
                    End(false, reason);
                    return;
                }

                var receipt = batch.Receipts[0];
                var hit = receipt.Target;
                if (receipt.Outcome.Reason != HitReason.Applied)
                {
                    End(false, "目标未受理命中：" + receipt.Outcome.Reason);
                    return;
                }
                if (hit == null || !hit.IsAvailable || hit.Faction == CombatFaction.Neutral ||
                    hit.Faction == Context.owner.Vitals.Faction || receipt.Outcome.HpAfter <= 0 ||
                    receipt.TargetLifeRevision != hit.LifeRevision)
                {
                    End(false, "目标身份不合法，或本次命中后已失效");
                    return;
                }

                target = hit;
                targetLife = hit.LifeRevision;
                pullFrom = Context.owner.transform.position;
                var offset = hit.WorldPosition - pullFrom;
                if (Mathf.Abs(offset.y) > parameters.heightTolerance || offset.x * facing < -parameters.dockTolerance ||
                    Mathf.Abs(offset.x) > delivery.range + delivery.collisionRadius)
                {
                    End(false, "目标不在有效回拉空间");
                    return;
                }

                // 交叠安全距离取双方实际胶囊边界和作者停靠距离中较大者。目标只由共同运动器移动。
                float spacing = parameters.dockDistance;
                if (Context.owner.TryGetComponent<CharacterController>(out var sourceBody) &&
                    hit.TryGetComponent<CharacterController>(out var targetBody))
                {
                    var sourceBounds = sourceBody.bounds;
                    var targetBounds = targetBody.bounds;
                    float sourceEdge = sourceBounds.extents.x + (sourceBounds.center.x - pullFrom.x) * facing;
                    float targetEdge = targetBounds.extents.x - (targetBounds.center.x - hit.WorldPosition.x) * facing;
                    spacing = Mathf.Max(spacing, sourceEdge + targetEdge + parameters.dockTolerance + .03f);
                }
                if (offset.x * facing < spacing - parameters.dockTolerance)
                {
                    End(false, "目标过近，已越过安全停靠点");
                    return;
                }
                var dock = (Vector2)pullFrom + Vector2.right * (facing * spacing);
                targetAttachment = hit.transform.InverseTransformPoint(receipt.Contact.Position);
                if (!ActionImpactMotor.TryMoveTarget(hit, Context.id, dock, parameters.pullSpeed,
                    parameters.pullTimeout, parameters.dockTolerance, parameters.heightTolerance, out motion, out var rejection))
                {
                    End(false, rejection ?? "目标运动拒绝");
                    return;
                }

                phase = Phase.Pulling;
                deadline = Context.owner.SimulationTime + parameters.pullTimeout;
                Context.ReportBehavior("正在回拉", target, $"停靠 X {dock.x:0.00}");
                ReadMotion();
            }

            public override void Tick(float delta, bool held)
            {
                if (Ended) return;
                if (!Context.CanContinueBehavior)
                {
                    End(false, "行为身份或接段期限已失效");
                    return;
                }
                if (phase == Phase.WaitingForRelease) return;
                if (phase == Phase.Pulling)
                {
                    if (target == null || !target.IsAvailable || target.LifeRevision != targetLife)
                    {
                        End(false, "回拉目标生命已失效");
                        return;
                    }
                    if (((Vector2)(Context.owner.transform.position - pullFrom)).sqrMagnitude >
                        parameters.maxOwnerDrift * parameters.maxOwnerDrift)
                    {
                        End(false, "来源离开回拉起点");
                        return;
                    }
                    if (ReadMotion()) return;
                    view?.Update(target.transform.TransformPoint(targetAttachment));
                }
                if (Context.owner.SimulationTime >= deadline)
                    End(false, phase == Phase.Pulling ? "回拉超过期限" : "飞行未收到结算回执");
            }

            private bool ReadMotion()
            {
                switch (motion.Status)
                {
                    case ActionImpactMotor.TargetMotionStatus.Arrived:
                        End(true, "目标已实际到位");
                        return true;
                    case ActionImpactMotor.TargetMotionStatus.Failed:
                    case ActionImpactMotor.TargetMotionStatus.Cancelled:
                        End(false, motion.Reason ?? "回拉已中止");
                        return true;
                }
                Context.ReportBehavior("正在回拉", target, $"请求 {motion.RequestedStep.x:0.000} · 实移 {motion.ActualStep.x:0.000}");
                return false;
            }

            private void ObserveFlight(Vector3 position)
            {
                if (!Ended && phase == Phase.Flying) view?.Update(position);
            }

            private void End(bool success, string reason)
            {
                if (Ended) return;
                Ended = true;
                DisposeOwned();
                Context.CompleteBehavior(success, reason, target);
            }

            public override void Cancel()
            {
                if (Ended) return;
                End(false, "来源动作取消");
                base.Cancel();
            }

            private void DisposeOwned()
            {
                motion?.Cancel();
                motion = null;
                view?.Dispose();
                view = null;
            }
        }

        // 使用真实投递位置与接触点驱动占位链线。握持点优先复用当前装备已标定的源骨骼。
        private sealed class ChainView
        {
            private readonly CharacterRoot owner;
            private readonly float facing;
            private readonly GameObject root;
            private readonly Material material;
            private readonly LineRenderer cord, blade;
            private readonly SkeletonAnimation body;
            private readonly ResolvedRigSocket socket;
            private readonly Spine.SkeletonData socketModel;

            public static ChainView Create(CharacterRoot owner, float facing)
            {
                var shader = Shader.Find("Sprites/Default");
                return shader == null ? null : new ChainView(owner, facing, shader);
            }

            private ChainView(CharacterRoot owner, float facing, Shader shader)
            {
                this.owner = owner;
                this.facing = facing;
                root = new GameObject("链匕首 · 运行时占位") { hideFlags = HideFlags.DontSave };
                if (owner.gameObject.scene.isLoaded) SceneManager.MoveGameObjectToScene(root, owner.gameObject.scene);
                material = new Material(shader) { name = "链匕首 · 临时线材质", hideFlags = HideFlags.DontSave };
                cord = AddLine("锁链", .036f, .026f, new Color(.36f, .73f, .86f, 1));
                blade = AddLine("匕首", .10f, .015f, new Color(.81f, .94f, 1, 1));

                body = owner.GetComponentInChildren<CharacterSpineView>()?.Skeleton;
                if (owner.TryGetComponent<CharacterEquipment>(out var equipment))
                {
                    var semantic = equipment.RightDefinition?.spinePresentation?.semanticSocketId;
                    var rig = owner.Definition?.rigProfile;
                    if (rig != null && body?.Skeleton?.Data != null && !string.IsNullOrEmpty(semantic))
                    {
                        socketModel = body.Skeleton.Data;
                        try { socket = rig.Resolve(semantic, socketModel); }
                        catch (ArgumentException) { /* 未标定的角色保留身体锚点供试打。 */ }
                    }
                }
            }

            private LineRenderer AddLine(string name, float width, float tipWidth, Color color)
            {
                var part = new GameObject(name);
                part.transform.SetParent(root.transform, false);
                var line = part.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.sharedMaterial = material;
                line.startWidth = width;
                line.endWidth = tipWidth;
                line.startColor = line.endColor = color;
                line.sortingOrder = 60;
                return line;
            }

            public void Update(Vector3 end)
            {
                var grip = owner.transform.position + Vector3.up * .8f + Vector3.right * (.25f * facing);
                if (socket != null && body != null && ReferenceEquals(socketModel, body.Skeleton?.Data))
                {
                    var pose = body.Skeleton.Bones.Items[socket.BoneIndex].AppliedPose;
                    grip = body.transform.TransformPoint(socket.Position(pose));
                }
                // 同一真实接触点只作画面锚点。骨骼与匕首展示不会推写目标位置。
                var bladeBase = end - Vector3.right * (.22f * facing);
                cord.SetPosition(0, grip);
                cord.SetPosition(1, bladeBase);
                blade.SetPosition(0, bladeBase);
                blade.SetPosition(1, end);
            }

            public void Dispose()
            {
                if (root != null)
                {
                    root.SetActive(false);
                    UnityEngine.Object.Destroy(root);
                }
                if (material != null) UnityEngine.Object.Destroy(material);
            }
        }
    }
}
