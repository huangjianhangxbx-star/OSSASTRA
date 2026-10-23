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
