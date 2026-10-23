using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class StateRequest
{
    /// <summary>请求类型</summary>
    public CharacterStateType characterStateType { get; protected set; }
    //protected set 意味着：
    //只有它自己或者它的子类（ CharacterStateRequest）可以修改这个值。
    //InputHandler 不属于它的子类，所以不能修改。
    //通过在子类的构造函数中赋值，就完美解决了权限和抽象类无法实例化的问题。


    /// <summary>请求优先级（可选，默认使用状态机优先级）</summary>
    public int OverridePriority { get; protected set; } = -1;

    /// <summary>请求是否紧急（紧急请求可以打断非紧急的）</summary>
    public bool IsUrgent { get; protected set; } = false;

    /// <summary>请求发起时间（用于超时处理）</summary>
    public float RequestTime { get; private set; }

    /// <summary>请求数据（具体内容由子类定义）</summary>
    public object Data { get; protected set; }

    public System.Action onActionPoint; // 回调

    public StateRequest()
    {
        RequestTime = Time.time;
    }
}




