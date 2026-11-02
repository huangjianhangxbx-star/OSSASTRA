/*
 * 功能：携带作者登记房间在一个主控脚点下的精确/进入/保留空间采样。
 * 边界：只含值数据；不拥有角色位置、物理体积、输入、Renderer或时间。
 * 协作：运行适配层负责几何采样，RoomMembershipResolver负责身份与滞回选择。
 */
namespace Ossastra.SceneVisibility.Rooms.Core
{
    public readonly struct RoomCandidate
    {
        public string RoomId { get; }
        public int Priority { get; }
        public bool Inside { get; }
        public bool CanEnter { get; }
        public bool CanRetain { get; }

        public RoomCandidate(string roomId, int priority, bool inside, bool canEnter, bool canRetain)
        {
            RoomId = roomId;
            Priority = priority;
            Inside = inside;
            CanEnter = canEnter;
            CanRetain = canRetain;
        }
    }
}
