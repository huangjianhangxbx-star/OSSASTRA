/* 功能：为正式游戏时间提供只读输入缝；环境根不推进或改写外部日历。 */
namespace Ossastra.EnvironmentSimulation
{
 public interface IEnvironmentTimeSource{bool TryReadHours(out double hours);}
 public enum EnvironmentTimeMode{Manual,PreviewClock,External}
 public enum EnvironmentContribution{Original,SunOnly,AmbientOnly,CloudOnly,Combined}
}
