using System;
using System.Reflection;
class FogTransitionTests
{
 static int count;
 static Type Type;
 static float Eval(float[] margins, double[] times, double now, float delay=0)
  => (float)Type.GetMethod("Evaluate").Invoke(null,new object[]{margins,times,now,.3f,.5f,.3f,delay});
 static void Near(string name,float value,float expected,float tolerance=.0002f)
 { if(Math.Abs(value-expected)>tolerance)throw new Exception(name+": expected "+expected+", got "+value); ++count; }
 static void Main()
 {
  Type=Assembly.GetExecutingAssembly().GetType("Ossastra.SceneVisibility.Rooms.Rendering.RoomFogTransitionMath");
  if(Type==null)throw new Exception("New legal area has no 30% entry / timed reveal behavior yet.");
  Near("new area begins at thirty percent",Eval(new[]{-1f,0f},new[]{0d,.001d},.001),.3f);
  Near("new area reaches full in half second",Eval(new[]{-1f,0f,1f},new[]{-.001d,0d,.5d},.5),1);
  Near("stable legal area remains full",Eval(new[]{1f,1f,1f},new[]{0d,.2d,.4d},.4),1);
  Near("new coverage inside same portal also fades",Eval(new[]{-1f,0f,1f},new[]{-.001d,0d,.25d},.25),.9125f);
  Near("loss starts at previous value",Eval(new[]{1f,-.000001f},new[]{0d,.000001d},.000001),1,.0001f);
  Near("lost static environment reaches black",Eval(new[]{1f,0f,-1f},new[]{-.001d,0d,.3d},.3),0);
  float before=Eval(new[]{-1f,0f,1f},new[]{-.001d,0d,.2d},.2);
  float reverse=Eval(new[]{-1f,0f,1f,-.000001f},new[]{-.001d,0d,.2d,.200001d},.200001);
  Near("reversal carries current brightness",reverse,before,.001f);
  Near("spread still completes at half second",Eval(new[]{-1f,0f,1f},new[]{-.001d,0d,.5d},.5,.12f),1);
  Near("no historical visibility grants a currently denied dynamic",(float)Type.GetMethod("DynamicVisibility").Invoke(null,new object[]{false,1f}),0);
  Near("feather never expands outside hard permit",(float)Type.GetMethod("InwardFeather").Invoke(null,new object[]{-.01f,.35f}),0);
  Near("feather is fully clear well inside",(float)Type.GetMethod("InwardFeather").Invoke(null,new object[]{1f,.35f}),1);
  var footprint=Type.GetMethod("FootprintMargin");
  if(footprint==null)throw new Exception("A floor on the room lower bound must not be blackened as a vertical fog edge.");
  Near("floor boundary remains visible inside horizontal footprint",(float)footprint.Invoke(null,new object[]{4f,0f,4f}),4);
  Near("horizontal feather does not grant a point below volume",(float)footprint.Invoke(null,new object[]{4f,-1f,4f}),-1);
  Console.WriteLine("PASS "+count+" fog transition behavior checks");
 }
}
