using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class FlowRequest
{
    public FlowRequest()
    {
        RequestTime = Time.time;
    }

    public GameFlowStateType flowType { get; protected set; }


    /// <summary>请求发起时间（用于超时处理）</summary>
    public float RequestTime { get; private set; }

    /// <summary>请求数据（具体内容由子类定义）</summary>
    public object Data { get; protected set; }



}
