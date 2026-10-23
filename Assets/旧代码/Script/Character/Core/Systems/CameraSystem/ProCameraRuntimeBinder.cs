using Com.LuisPedroFonseca.ProCamera2D;
using UnityEngine;

public class ProCameraRuntimeBinder : MonoBehaviour
{
    [SerializeField] private ProCamera2D proCamera2D;

    private void Awake()
    {
        if (proCamera2D == null)
        {
            proCamera2D = FindObjectOfType<ProCamera2D>();
        }

        if (proCamera2D == null)
        {
            Debug.LogError("[ProCameraRuntimeBinder] 场景中没有找到 ProCamera2D");
        }
    }

    public void BindTarget(Transform target)
    {
        if (target == null)
        {
            Debug.LogError("[ProCameraRuntimeBinder] 绑定失败：target 为 null");
            return;
        }

        if (proCamera2D == null)
        {
            Debug.LogError("[ProCameraRuntimeBinder] 绑定失败：proCamera2D 为 null");
            return;
        }

        proCamera2D.RemoveAllCameraTargets();
        proCamera2D.AddCameraTarget(target);

        //Debug.Log($"[ProCameraRuntimeBinder] 已绑定相机目标：{target.name}");
    }
}