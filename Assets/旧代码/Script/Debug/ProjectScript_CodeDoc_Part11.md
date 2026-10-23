# ProjectScript Code Documentation - Part 11

> Source: `E:\WORLDCREATOR\new\Project_VELGITA\Assets\Script`  |  Total files: 212  |  This part: 12

## Architecture summary

This module contains 212 supported source files. The summary is inferred from directory layout, namespaces, declared types, and explicit asset references.

## File index

| File | Lines | Namespaces | Types | Asset refs |
|---|---:|---|---|---:|
| `Assets/Script/GameFlowManager/HuntFlow/HuntResultState.cs` | 52 | - | class HuntResultState | 0 |
| `Assets/Script/GameFlowManager/HuntFlow/HuntSpawnPlayerState.cs` | 40 | - | class HuntSpawnPlayerState | 0 |
| `Assets/Script/GameFlowManager/HuntFlow/IHuntSubsystem.cs` | 10 | - | interface IHuntSubsystem | 0 |
| `Assets/Script/GameFlowManager/IGameFlowState.cs` | 30 | - | interface IGameFlowState | 0 |
| `Assets/Script/GameFlowManager/IGameSubsystem.cs` | 15 | - | interface IGameSubsystem | 0 |
| `Assets/Script/Other/ReadOnlyAttribute.cs` | 5 | - | class ReadOnlyAttribute | 0 |
| `Assets/Script/Scene/FacingCamera.cs` | 49 | - | class FacingCamera | 0 |
| `Assets/Script/Scene/FacingCamera_Character.cs` | 102 | - | class FacingCamera_Character | 0 |
| `Assets/Script/Scene/PlatForm/Platform_Basic.cs` | 60 | - | class Platform_Basic | 0 |
| `Assets/Script/Scene/PlatForm/PlatformLogic.cs` | 70 | - | class PlatformLogic | 0 |
| `Assets/Script/Scene/PlatForm/PlatformManager.cs` | 52 | - | class PlatformManager | 0 |
| `Assets/Script/UnityGameEngineEditor/GMcmd.cs` | 31 | - | class GMcmd | 0 |

## Public API

### `Assets/Script/GameFlowManager/HuntFlow/HuntResultState.cs`
- `public class HuntResultState : BaseHuntFlowState
{`
- `public HuntResultState(GameFlowRoot root)
    {`
- `public override string huntStateName => "HuntResultState";`
- `public override GameFlowStateType FlowStateType => GameFlowStateType.HuntResult;`
- `public override void Enter()
    {`
- `public override void Exit()
    {`
- `public override bool CanTransitionTo(BaseHuntFlowState newState)
    {`
### `Assets/Script/GameFlowManager/HuntFlow/HuntSpawnPlayerState.cs`
- `public class HuntSpawnPlayerState : BaseHuntFlowState
{`
- `public HuntSpawnPlayerState(GameFlowRoot root)
    {`
- `public override string huntStateName => "HuntSpawnPlayerState";`
- `public override GameFlowStateType FlowStateType => GameFlowStateType.HuntSpawnPlayer;`
- `public override void Enter()
    {`
- `public override bool CanTransitionTo(BaseHuntFlowState newState)
    {`
### `Assets/Script/GameFlowManager/HuntFlow/IHuntSubsystem.cs`
- `public interface IHuntSubsystem
{`
### `Assets/Script/GameFlowManager/IGameFlowState.cs`
- `public interface IGameFlowState
{`
### `Assets/Script/GameFlowManager/IGameSubsystem.cs`
- `public interface IGameSubsystem
{`
### `Assets/Script/Other/ReadOnlyAttribute.cs`
- `public class ReadOnlyAttribute : PropertyAttribute
{`
### `Assets/Script/Scene/FacingCamera.cs`
- `public class FacingCamera : MonoBehaviour
{`
### `Assets/Script/Scene/FacingCamera_Character.cs`
- `public class FacingCamera_Character : MonoBehaviour
{`
- `public void SetFacingDirection(Vector3 direction)
    {`
- `public void TurnCharacter(bool faceRight)
    {`
- `public void SetTiltAngle(float angle)
    {`
- `public void FollowCameraX()
    {`
### `Assets/Script/Scene/PlatForm/Platform_Basic.cs`
- `public class Platform_Basic : MonoBehaviour
{`
- `public float width = 5f;`
- `public bool canFall = true;`
- `public float platfromPosiY => this.transform.position.y;`
- `public float platfromPosi_left => this.transform.position.x - width / 2;`
- `public float platfromPosi_right => this.transform.position.x + width / 2;`
- `public bool CheckObjectOnPlatform(Vector3 objectPosi)
    {`
- `public void Start()
    {`
### `Assets/Script/Scene/PlatForm/PlatformLogic.cs`
- `public class PlatformLogic
{`
### `Assets/Script/Scene/PlatForm/PlatformManager.cs`
- `public class PlatformManager : MonoBehaviour
{`
- `public static PlatformManager Instance
    {`
- `public List<Platform_Basic> platformList;`
- `public void AddPlatform(Platform_Basic platform)
    {`
- `public void RemovePlatform(Platform_Basic platform)
    {`
- `public void ClearData()
    {`
### `Assets/Script/UnityGameEngineEditor/GMcmd.cs`
- `public class GMcmd
{`

## Referenced assets

- No explicit `Assets/...` references detected.

## Complete source

### `Assets/Script/GameFlowManager/HuntFlow/HuntResultState.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HuntResultState : BaseHuntFlowState
{
    private readonly GameFlowRoot root;

    public HuntResultState(GameFlowRoot root)
    {
        this.root = root;
    }

    public override string huntStateName => "HuntResultState";
    public override GameFlowStateType FlowStateType => GameFlowStateType.HuntResult;
    public override void Enter()
    {

 
        var hunt = root.huntFlow;

        Debug.Log("[RunResult] 一局结束");

 

        //run.dataBus.isRunActive = false;
        //run.dataBus.isPaused = true;

        //run.enemySpawnDirector.StopSpawn();
        //run.abilityDirector.StopAbilities();

        //Time.timeScale = 0f;

        //run.runUIWindow.ShowResult(run.dataBus);

    }

   

    public override void Exit()
    {
        Debug.Log("[RunCombat] 离开战斗循环");
    }
    public override bool CanTransitionTo(BaseHuntFlowState newState)
    {

        return newState is HuntLevelUpState; //* || newState is HuntBossState || newState is HuntResultState;*/


    }
}
```

### `Assets/Script/GameFlowManager/HuntFlow/HuntSpawnPlayerState.cs`

```cs
using Com.LuisPedroFonseca.ProCamera2D;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HuntSpawnPlayerState : BaseHuntFlowState
{
    private readonly GameFlowRoot root;

    public HuntSpawnPlayerState(GameFlowRoot root)
    {
        this.root = root;
    }

    public override string huntStateName => "HuntSpawnPlayerState";
    public override GameFlowStateType FlowStateType => GameFlowStateType.HuntSpawnPlayer;

    public override void Enter()
    {
        CharacterRoot player = root.huntFlow.huntPlayerSpawner.SpawnPlayer();




        if (player == null)
        {
            Debug.LogError("[HuntSpawnPlayerState] 玩家生成失败");
            return;
        }

        huntFlowStateMachine.TransitionTo<HuntCombatState>();

    }

    public override bool CanTransitionTo(BaseHuntFlowState newState)
    {
        return newState is HuntCombatState;
    }
}
```

### `Assets/Script/GameFlowManager/HuntFlow/IHuntSubsystem.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IHuntSubsystem
{
    void Initialize(HuntFlowRoot root);

}
```

### `Assets/Script/GameFlowManager/IGameFlowState.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IGameFlowState
{
    string flowStateMachineName { get; }

    /// <summary>当前是否活跃（想执行）</summary>
    bool IsActive { get; }

    /// <summary>状态优先级（数值越大优先级越高）</summary>
    int Priority { get; }
    /// <summary>当前状态名称（用于调试）</summary>
    string currentFlowStateName { get; }

    void Initialize(GameFlowDataBus gameFlowDataBus,GameFlowRoot root );
    void Enter();

    /// <summary>退出状态机（被仲裁器取消时调用）</summary>
    void Exit();

    /// <summary>每帧更新（只有活跃状态机会被调用）</summary>
    void Update(float deltaTime);

    /// <summary>接收状态请求（外部调用）</summary>
    bool RequestState(object stateData);//需要写入回应状态的条件

}
```

### `Assets/Script/GameFlowManager/IGameSubsystem.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IGameSubsystem
{


    // 只给一个入口：总店长
    void Initialize(GameFlowRoot root);



}
```

### `Assets/Script/Other/ReadOnlyAttribute.cs`

```cs
using UnityEngine;

public class ReadOnlyAttribute : PropertyAttribute
{
}
```

### `Assets/Script/Scene/FacingCamera.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FacingCamera : MonoBehaviour
{

    Transform[] childs;









    // Start is called before the first frame update
    void Start()
    {
        childs = new Transform[transform.childCount];
        for(int i = 0; i<transform.childCount; i++)
        {
            childs[i] = transform.GetChild(i);


        }



        
    }

    // Update is called once per frame
    void Update()
    {
        for(int i = 0; i<childs.Length;i++)
        {

            childs[i].rotation = Camera.main.transform.rotation;


        }



    }
}
```

### `Assets/Script/Scene/FacingCamera_Character.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FacingCamera_Character : MonoBehaviour
{
    [Header("倾斜设置")]
    [SerializeField] private float tiltAngle = -45f;  // X轴倾斜角度

    // ================= 旋转存储 =================
    // 关键：使用两个独立的四元数存储不同的旋转分量
    private Quaternion tiltRotation;  // 存储X轴倾斜（摄像机视角）
    private Quaternion turnRotation;  // 存储Y轴转身（左右方向）

    // ================= 初始化 =================
    void Start()
    {
        // 初始化倾斜旋转：只绕X轴旋转（俯仰）
        tiltRotation = Quaternion.Euler(tiltAngle, 0, 0);

        // 初始化转身旋转：初始面向右边（Y轴0度）
        turnRotation = Quaternion.identity;  // identity = 无旋转

        // 应用初始旋转
        UpdateRotation();
    }

    // ================= 核心方法：更新旋转 =================
    /// <summary>
    /// 更新最终旋转（关键！避免万向锁的核心方法）
    /// 原理：使用四元数乘法组合多个旋转，而不是欧拉角
    /// </summary>
    private void UpdateRotation()
    {
        // ====== 关键原理说明 ======
        // transform.rotation = tiltRotation * turnRotation;
        // 
        // 四元数乘法顺序：从右往左应用
        // 1. 先应用 turnRotation（Y轴转身）
        // 2. 再应用 tiltRotation（X轴倾斜）
        // 
        // 视觉顺序：
        //   角色先转身面向目标方向（Y轴旋转）
        //   然后摄像机让角色倾斜（X轴旋转）
        //   
        // 为什么这样能避免万向锁？
        // - 欧拉角（Euler）有旋转顺序问题（Unity默认是Z→X→Y）
        // - 当X轴接近90度时，Z轴和Y轴会重合，导致万向锁
        // - 四元数没有顺序问题，直接描述3D空间中的旋转

        transform.rotation = tiltRotation * turnRotation;
    }

    // ================= 外部调用接口 =================
    /// <summary>
    /// 设置角色面向方向
    /// 注意：这里的方向是在倾斜前的2D平面上的方向
    /// </summary>
    public void SetFacingDirection(Vector3 direction)
    {
        // 计算Y轴转身角度
        // 重要：这里的方向是在水平2D平面上的方向判断
        float yAngle = direction == Vector3.left ? 180f : 0f;

        // 设置转身旋转（只修改Y轴）
        turnRotation = Quaternion.Euler(0, yAngle, 0);

        // 更新最终旋转
        UpdateRotation();
    }

    /// <summary>
    /// 直接控制转身方向
    /// </summary>
    public void TurnCharacter(bool faceRight)
    {
        float yAngle = faceRight ? 0f : 180f;
        turnRotation = Quaternion.Euler(0, yAngle, 0);
        UpdateRotation();
    }

    /// <summary>
    /// 设置倾斜角度
    /// </summary>
    public void SetTiltAngle(float angle)
    {
        tiltAngle = angle;
        tiltRotation = Quaternion.Euler(tiltAngle, 0, 0);
        UpdateRotation();
    }

    /// <summary>
    /// 跟随摄像机X轴旋转
    /// </summary>
    public void FollowCameraX()
    {
        float cameraX = Camera.main.transform.rotation.eulerAngles.x;
        tiltRotation = Quaternion.Euler(cameraX, 0, 0);
        UpdateRotation();
    }
}
```

### `Assets/Script/Scene/PlatForm/Platform_Basic.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class Platform_Basic : MonoBehaviour
{

    public float width = 5f;
    public bool canFall = true;

    public float platfromPosiY => this.transform.position.y;

    public float platfromPosi_left => this.transform.position.x - width / 2;
    public float platfromPosi_right => this.transform.position.x + width / 2;

    // Start is called before the first frame update

    public bool CheckObjectOnPlatform(Vector3 objectPosi)
    {
        if (objectPosi.y >= platfromPosiY && objectPosi.x >= platfromPosi_left && objectPosi.x <= platfromPosi_right)
        {


            return true;


        }
        return false;

    }





    public void Start()
    {
        
        PlatformManager.Instance.AddPlatform(this);

    }




    private void OnDrawGizmos()
    {
        
        Gizmos.color = Color.red;

        Gizmos.DrawLine(this.transform.position - Vector3.right * width/2,this.transform.position + Vector3.right * width/2);


    }



}
```

### `Assets/Script/Scene/PlatForm/PlatformLogic.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlatformLogic
{

    ////public PlayerControl playerObj;


    //public JumpModule playerObj;

    ////public AnimationHandler animationHandler;


    //public Platform_Basic nowPlatform;

    //private List<Platform_Basic> platformData;





    //public PlatformLogic(JumpModule playerobj)
    //{
    //    this.playerObj = playerobj;
    //    nowPlatform = null;

    //    platformData = PlatformManager.Instance.platformList;


    //}

    ////public PlatformLogic(AnimationHandler Handler)
    ////{
    ////    this.animationHandler = Handler;
    ////    nowPlatform = null;

    ////    platformData = PlatformManager.Instance.platformList;


    ////}


    //public void UpdateCheck()
    //{

    //    if(playerObj.isJump)
    //    {
            
    //        for (int i = 0; i < platformData.Count; i++)
    //        {
    //            if (platformData[i].CheckObjectOnPlatform(playerObj.transform.position) && ( nowPlatform == null || nowPlatform.platfromPosiY < platformData[i].platfromPosiY))
    //            {
    //                Debug.Log("sss");
    //                nowPlatform = platformData[i];
    //                playerObj.changePlatformData(nowPlatform.platfromPosiY, nowPlatform.canFall);

    //            }
    //        }
    //    }
    //}






}
```

### `Assets/Script/Scene/PlatForm/PlatformManager.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlatformManager : MonoBehaviour
{


    private static PlatformManager instance;

    public static PlatformManager Instance
    {
        get
        {

            if (instance == null)
                instance = new PlatformManager();
            return instance;

        }


    }


    public List<Platform_Basic> platformList;

    public void AddPlatform(Platform_Basic platform)
    {

        platformList.Add(platform);


    }
    public void RemovePlatform(Platform_Basic platform)
    {
        if(platformList.Contains(platform))
        {
            platformList.Remove(platform);
        }
    }

    public void ClearData()
    {

        platformList.Clear();

    }

    
}
```

### `Assets/Script/UnityGameEngineEditor/GMcmd.cs`

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


public class GMcmd
{


    //[MenuItem("GMcmd/读取表格")]
    
    //public static void ReadTable()
    //{

    //    PackageTableSO packageTable = Resources.Load<PackageTableSO>("Weapon/PackageTable");

    //    foreach(PackageTableItem packageTableItem in packageTable.DataList)
    //    {

    //        Debug.Log(string.Format("[id]:{0},[Name]:{1}", packageTableItem.id, packageTableItem.name));

    //    }

    //}




}
```
