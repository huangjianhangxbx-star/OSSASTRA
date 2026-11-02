/*
 * 功能：承接唯一主控的稳定观察点、逻辑朝向与实际房间身份。
 * 边界：房间解析器与角色根拥有这些值；相机位置不能成为观察授权。
 * 生命周期：单次求解的不可变快照，不缓存发现历史或改变角色状态。
 */
namespace Ossastra.SceneVisibility.Rooms.Core
{
    public readonly struct ObserverSnapshot
    {
        public const string Exterior = "@exterior";
        public string SpaceId { get; }
        public SightVector3 Position { get; }
        public SightVector3 Forward { get; }
        public ObserverSnapshot(string spaceId, SightVector3 position, SightVector3 forward)
        {
            SpaceId = NormalizeSpace(spaceId); Position = position; Forward = forward;
        }
        internal static string NormalizeSpace(string id) => string.IsNullOrWhiteSpace(id) ? Exterior : id;
    }
}
