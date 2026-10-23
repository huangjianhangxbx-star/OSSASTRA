using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 这是一个具体的实现类，不再是 abstract,因为 StateRequest的abstract属性导致无法用new构筑实例，而输入系统驱动下推自动机的机制需要这个实例
public class CharacterStateRequest : StateRequest
{
    // 通过构造函数给父类的 protected 属性赋值
    //构造函数是一种特殊的方法，特点：
    //名称必须和类名完全相同
    //没有返回值（连 void 都不用写）
    //在创建对象时自动调用
    //public CharacterStateRequest(CharacterStateType type, bool isUrgent = false, object data = null) : base()
    //{
    //    this.characterStateType = type;
    //    this.IsUrgent = isUrgent;
    //    this.Data = data;
    //}

    //增加回调

    public CharacterStateRequest(CharacterStateType type,bool isUrgent = false,object data = null,System.Action onActionPoint = null) : base()
    {
        this.characterStateType = type;
        this.IsUrgent = isUrgent;
        this.Data = data;
        // 关键：由于父类已经定义了该变量，这里直接赋值
        this.onActionPoint = onActionPoint;
    }




    // 如果需要覆盖优先级，可以再加一个构造函数或参数(重载）
    //public CharacterStateRequest(CharacterStateType type, int priority, bool isUrgent = false) : base()
    //{
    //    this.characterStateType = type;
    //    this.OverridePriority = priority;
    //    this.IsUrgent = isUrgent;
    //}


    public void ExecuteCombat()
    {

    }

}
