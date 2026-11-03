/* 功能：提供环境演示时刻的纯数值归一化与推进，不拥有游戏日历或暂停权限。 */
using System;
namespace Ossastra.EnvironmentSimulation
{
 public static class EnvironmentClock
 {
  public static double WrapHours(double hours)
  {
   if(double.IsNaN(hours)||double.IsInfinity(hours))throw new ArgumentException("环境时刻必须是有限值。",nameof(hours));
   double wrapped=hours%24d;return wrapped<0?wrapped+24d:wrapped;
  }
  public static double AdvanceHours(double hours,double deltaSeconds,double cycleSeconds)
  {
   if(double.IsNaN(cycleSeconds)||double.IsInfinity(cycleSeconds)||cycleSeconds<=0)throw new ArgumentException("环境周期必须为正有限秒数。",nameof(cycleSeconds));
   if(double.IsNaN(deltaSeconds)||double.IsInfinity(deltaSeconds)||deltaSeconds<0)throw new ArgumentException("环境步长必须为非负有限秒数。",nameof(deltaSeconds));
   return WrapHours(WrapHours(hours)+(deltaSeconds%cycleSeconds)/cycleSeconds*24d);
  }
 }
}
