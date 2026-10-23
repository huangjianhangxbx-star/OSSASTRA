using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 具体的状态请求实现
/// </summary>
public class TurnAroundRequest : StateRequest
{
    public Vector3 TargetDirection { get; private set; }

    public TurnAroundRequest(Vector3 direction)
    {
        characterStateType = CharacterStateType.TurnAround;
        TargetDirection = direction;
        Data = direction; // 同时存到Data中
    }
}
