using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Ossastra.SceneVisibility.Rooms;
internal static class PresentationRegistryTests
{
    static int passed, failed;
    static readonly List<RoomPresentationRegistry.Entry> output=new List<RoomPresentationRegistry.Entry>();
    static void Assert(bool condition,string message) { if(!condition)throw new Exception(message); }
    static void Check(string name,Action body) { try{body();Console.WriteLine("PASS "+name);passed++;}catch(Exception e){Console.WriteLine("FAIL "+name+": "+e.Message);failed++;} }
    static void Read() { output.Clear();RoomPresentationRegistry.Append(output); }
    static MethodInfo NewRegister()=>typeof(RoomPresentationRegistry).GetMethod("Register",new[]{typeof(object),typeof(Renderer),typeof(Vector3),typeof(bool),typeof(Transform),typeof(bool)});
    static void RegisterNew(object owner,Renderer r,Vector3 position,bool partial,Transform follow=null,bool gate=false)
    {
        var method=NewRegister();Assert(method!=null,"缺少动态锚点/片元裁剪登记合同");
        method.Invoke(null,new object[]{owner,r,position,partial,follow,gate});
    }
    static bool Flag(RoomPresentationRegistry.Entry entry,string name)
    { var field=typeof(RoomPresentationRegistry.Entry).GetField(name);Assert(field!=null,"缺少 "+name+" 许可值");return (bool)field.GetValue(entry); }
    static int Main()
    {
        Check("旧掉落登记仍按原固定锚且owner不可抢占",()=>{
            var owner=new object();var other=new object();var r=new Renderer();
            RoomPresentationRegistry.Register(owner,r,new Vector3(2,0,3));RoomPresentationRegistry.Register(other,r,new Vector3(99,0,99));
            Read();Assert(output.Count==1&&output[0].Anchor.x==2,"另一个owner覆盖了原登记");
            RoomPresentationRegistry.Release(other,r);Read();Assert(output.Count==1,"非owner撤销了登记");
            RoomPresentationRegistry.Release(owner,r);Read();Assert(output.Count==0,"原owner未能撤销");
        });
        Check("短命Renderer销毁后清理持有引用而不越积越多",()=>{
            var owner=new object();var r=new Renderer();RoomPresentationRegistry.Register(owner,r,new Vector3());r.Destroyed=true;
            Read();Assert(output.Count==0,"已销毁对象仍输出");
            var map=(IDictionary)typeof(RoomPresentationRegistry).GetField("entries",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            Assert(map.Count==0,"Append跳过fake-null但仍永久持有字典key");
            RoomPresentationRegistry.Release(owner);
        });
        Check("FX传递强制片元裁剪且跟随当前世界锚",()=>{
            var owner=new object();var r=new Renderer();var follow=new Transform{position=new Vector3(1,1,1)};
            try {
                RegisterNew(owner,r,new Vector3(8,8,8),true,follow);
                follow.position=new Vector3(7,2,9);Read();Assert(output.Count==1&&output[0].Anchor.x==7&&output[0].Anchor.z==9,"锚点仍停在发射点");
                Assert(Flag(output[0],"ForcePartial"),"FX丢失片元裁剪策略");
            }finally{RoomPresentationRegistry.Release(owner);}
        });
        Check("未知信息条传递本帧锚点许可需求",()=>{
            var owner=new object();var r=new Renderer();
            try { RegisterNew(owner,r,new Vector3(),true,null,true);Read();Assert(output.Count==1&&Flag(output[0],"RequireAnchorPermission"),"信息条未要求空间许可"); }
            finally{RoomPresentationRegistry.Release(owner);}
        });
        Check("跟随锚点销毁后不回落出生位置授权",()=>{
            var owner=new object();var r=new Renderer();var follow=new Transform();
            try { RegisterNew(owner,r,new Vector3(9,0,9),false,follow);follow.Destroyed=true;Read();Assert(output.Count==0,"失去角色锚点后仍按旧出生位置放行"); }
            finally{RoomPresentationRegistry.Release(owner);}
        });
        Check("叶适配器禁用撤销返场重新登记且销毁精确释放",()=>{
            var type=typeof(RoomPresentationRegistry).Assembly.GetType("Ossastra.SceneVisibility.Rooms.RoomPresentationBinding");
            Assert(type!=null,"缺少随原visual生命周期释放的叶适配器");
            var binding=Activator.CreateInstance(type);var r=new Renderer();
            type.GetMethod("Configure").Invoke(binding,new object[]{r,new Vector3(4,0,5),true,null,false});
            Read();Assert(output.Count==1,"配置后没有登记");
            type.GetMethod("OnDisable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(binding,null);Read();Assert(output.Count==0,"禁用未撤销");
            type.GetMethod("OnEnable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(binding,null);Read();Assert(output.Count==1,"再次启用未重登");
            type.GetMethod("OnDestroy",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(binding,null);Read();Assert(output.Count==0,"销毁未精确释放");
        });
        Console.WriteLine("RESULT "+passed+" passed, "+failed+" failed");return failed==0?0:1;
    }
}
