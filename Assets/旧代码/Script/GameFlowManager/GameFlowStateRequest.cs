using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameFlowStateRequest : FlowRequest
{

    // 通过构造函数给父类的 protected 属性赋值
    //构造函数是一种特殊的方法，特点：
    //名称必须和类名完全相同
    //没有返回值（连 void 都不用写）
    //在创建对象时自动调用
    public GameFlowStateRequest(GameFlowStateType type,object data = null) : base()
    {
        this.flowType = type;
        //this.IsUrgent = isUrgent;
        this.Data = data;
    }





}
