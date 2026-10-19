/*
 * 脚本：CharacterIntentPolicy
 * 功能：从会话许可和来源可用性中选唯一Player／Companion／Neutral。
 * 边界：无设备、场景或动作状态；只返回选择结果，不拥有第二个Current。
 * 协作：运行时CharacterIntentSelector调用；纯合同可独立验证权威边界。
 */
namespace Ossastra.CharacterCore
{
    public enum CharacterIntentKind { Neutral, Player, Companion }
    public static class CharacterIntentPolicy
    {
        public static CharacterIntentKind Select(bool hasSession, bool sessionAllows, bool isCurrent, bool hasCompanion)
        {
            if (!hasSession) return CharacterIntentKind.Player;
            // 无合法Current或处于移交屏障时，两条来源都不生效。
            if (!sessionAllows) return CharacterIntentKind.Neutral;
            if (isCurrent) return CharacterIntentKind.Player;
            return hasCompanion ? CharacterIntentKind.Companion : CharacterIntentKind.Neutral;
        }
    }
}
