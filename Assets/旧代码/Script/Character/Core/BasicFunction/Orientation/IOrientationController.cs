using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IOrientationController
{
    //void Initialize(CharacterDataBus dataBus);
    void Update(float deltaTime);
    Vector3 GetCurrentDirection();
    //bool ShouldOverrideOtherControllers();
}
