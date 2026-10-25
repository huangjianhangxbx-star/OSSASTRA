/* 脚本：AbilityFeedbackFact
 * 功能：冻结一次已发生能力反馈的执行、定义、来源、操作与实际投递上下文。
 * 边界：只读事实，不付款、不推进动作、不选择声音；资源反馈允许没有攻击执行身份。
 * 协作：CharacterAbilities在实际回调位置发布，观察者无需查找仍在运行的Run。
 */
using System;
using UnityEngine;
namespace Ossastra.Characters
{
    public sealed class AbilityFeedbackFact
    {
        public Guid ExecutionId { get; }
        public string DefinitionId { get; }
        public string SourceInstanceId { get; }
        public string Kind { get; }
        public Vector3 Position { get; }
        public int EmissionIndex { get; }
        public string Channel { get; }
        public string DeliveryMode { get; }
        public string RequestSource { get; }
        public string Operation { get; }

        public AbilityFeedbackFact(Guid executionId, string definitionId, string sourceInstanceId, string kind,
            Vector3 position, int emissionIndex = -1, string channel = "", string deliveryMode = "",
            string requestSource = "", string operation = "")
        {
            ExecutionId = executionId;
            DefinitionId = definitionId ?? "";
            SourceInstanceId = sourceInstanceId ?? "";
            Kind = kind;
            Position = position;
            EmissionIndex = emissionIndex;
            Channel = channel ?? "";
            DeliveryMode = deliveryMode ?? "";
            RequestSource = requestSource ?? "";
            Operation = operation ?? "";
        }
    }
}
