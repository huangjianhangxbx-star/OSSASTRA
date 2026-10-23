using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 所有移动状态的基类
/// </summary>
public abstract class MovementState
{
    // 状态基本信息
    public abstract string StateName { get; }
    public abstract float MovementSpeed { get; }

    // 状态机引用
    protected MovementStateMachine stateMachine;
    protected CharacterDataBus dataBus;
    protected Transform transform;
    protected Rigidbody2D rigidbody;
    protected CharacterRoot characterRoot;



    //子状态机的初始化会被状态机调用
    public virtual void Initialize(MovementStateMachine machine, CharacterDataBus bus,Transform trans, Rigidbody2D rb,CharacterRoot root)
    {
        stateMachine = machine;
        dataBus = bus;
        transform = trans;
        rigidbody = rb;
        characterRoot = root;
    }

    // 状态生命周期
    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void Update(float deltaTime) { }
    public virtual void FixedUpdate(float fixedDeltaTime) { }

    // 状态转换检查
    public abstract bool CanTransitionTo(MovementState newState);
}
