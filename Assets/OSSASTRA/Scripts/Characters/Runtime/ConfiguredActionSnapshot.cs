/* 脚本：ConfiguredActionSnapshot
 * 功能：接受前冻结动作阶段的玩法时间线与语义映射结果。
 * 边界：不读取SO后续编辑，不拥有动作身份；Root与View分别消费玩法、表现数据。
 */
using Ossastra.CharacterCore;
namespace Ossastra.Characters
{
    public sealed class ConfiguredActionSnapshot
    {
        public BasicAttackDefinition Timeline {get;}
        public string Clip {get;}
        public float Mix {get;}
        public float Rate {get;}
        public bool Loop {get;}
        private readonly System.Func<object> copyPresentationPlan;
        public object CopyPresentationPlan()=>copyPresentationPlan?.Invoke();
        public ConfiguredActionSnapshot(BasicAttackDefinition timeline,string clip,float mix,float rate,bool loop,System.Func<object> copyPresentationPlan=null)
        {Timeline=timeline;Clip=clip;Mix=mix;Rate=rate;Loop=loop;this.copyPresentationPlan=copyPresentationPlan;}
    }
}
