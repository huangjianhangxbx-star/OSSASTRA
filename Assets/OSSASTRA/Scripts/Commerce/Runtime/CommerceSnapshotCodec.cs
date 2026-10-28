/* 脚本：CommerceSnapshotCodec
 * 功能：主线程将完整字段快照编码为冻结字节；精确保留普通物品的null装备、空引用和所有数字。
 * 边界：不处理活账本、文件或Unity对象；禁类型/引用元数据，未知字段及格式明确拒绝。
 */
using System;
using System.Text;
using Newtonsoft.Json;
namespace Ossastra.Commerce.Runtime
{
    public static class CommerceSnapshotCodec
    {
        private static JsonSerializerSettings Settings()=>new JsonSerializerSettings
        {TypeNameHandling=TypeNameHandling.None,MetadataPropertyHandling=MetadataPropertyHandling.Ignore,
            MissingMemberHandling=MissingMemberHandling.Error,ObjectCreationHandling=ObjectCreationHandling.Replace,
            DateParseHandling=DateParseHandling.None,MaxDepth=64};
        public static byte[] Encode(CommerceRuntimeSnapshot snapshot)
        {
            if(snapshot==null||snapshot.schema!=2)throw new ArgumentException("不支持的经济载荷版本");
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(snapshot,Settings()));
        }
        public static CommerceRuntimeSnapshot Decode(byte[] payload)
        {
            if(payload==null||payload.Length>64*1024*1024)throw new ArgumentException("经济载荷缺失或超限");
            var value=JsonConvert.DeserializeObject<CommerceRuntimeSnapshot>(Encoding.UTF8.GetString(payload),Settings());
            if(value==null||value.schema!=2)throw new ArgumentException("不支持的经济载荷版本");
            return value;
        }
    }
}
