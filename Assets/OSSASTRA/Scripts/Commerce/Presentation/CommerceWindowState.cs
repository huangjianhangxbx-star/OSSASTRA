/* 脚本：CommerceWindowState
 * 功能：保存交易窗口自己的选择、数字草稿和取消层级；不拥有钱币、库存或交易资格。
 * 协作：CommerceUIRoot使用本状态防止换物/改数量后继续确认旧报价；最终结算由宿主领域重验。
 */
namespace Ossastra.Commerce.Presentation
{
    public enum CommerceCancelResult { None, Quantity, Selection, Window }
    public sealed class CommerceWindowState
    {
        public string TargetId {get;private set;}
        public string SelectedId {get;private set;}
        public string QuoteId {get;private set;}
        public string QuantityText {get;private set;}="1";
        public bool IsOpen {get;private set;}
        public bool IsTrading {get;private set;}
        public bool IsBuying {get;private set;}
        public bool EditingQuantity {get;private set;}
        public int MaximumQuantity {get;private set;}
        public void OpenMenu(string targetId)
        {Close();TargetId=targetId;IsOpen=!string.IsNullOrEmpty(targetId);}
        public void OpenTrade(string targetId)
        {OpenMenu(targetId);IsTrading=IsOpen;}
        public void Select(string id,bool buying,int maximum)
        {
            if(SelectedId==id&&IsBuying==buying&&MaximumQuantity==maximum)return;
            SelectedId=id;IsBuying=buying;MaximumQuantity=maximum;QuantityText="1";QuoteId=null;EditingQuantity=false;
        }
        public void SetQuote(string quoteId) {QuoteId=quoteId;}
        public void SetQuantity(string value)
        {if(QuantityText==value)return;QuantityText=value??"";QuoteId=null;}
        public void BeginQuantityEdit() {EditingQuantity=true;}
        public CommerceCancelResult Cancel()
        {
            // 数字编辑、选物详情和会话分别消费一次取消，不能把同一按下继续传回世界。
            if(EditingQuantity){EditingQuantity=false;QuantityText="1";QuoteId=null;return CommerceCancelResult.Quantity;}
            if(SelectedId!=null){SelectedId=null;QuoteId=null;return CommerceCancelResult.Selection;}
            if(IsOpen){Close();return CommerceCancelResult.Window;}
            return CommerceCancelResult.None;
        }
        public void Close()
        {TargetId=null;SelectedId=null;QuoteId=null;QuantityText="1";IsOpen=false;IsTrading=false;IsBuying=false;EditingQuantity=false;MaximumQuantity=0;}
        public static bool TryQuantity(string text,int maximum,out int quantity,out string reason)
        {
            quantity=0;reason="数量必须是安全范围内的正整数";
            if(string.IsNullOrEmpty(text))return false;
            foreach(var c in text)if(c<'0'||c>'9')return false;
            if(!int.TryParse(text,out quantity)||quantity<=0)return false;
            if(quantity>maximum){reason="数量超过当前选中物品或库存上限";return false;}
            reason=null;return true;
        }
    }
}
