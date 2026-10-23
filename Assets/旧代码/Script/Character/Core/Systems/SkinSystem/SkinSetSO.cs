using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewSkinSet", menuName = "ScriptableObject/换装系统/皮肤组合")]
public class SkinSetSO : ScriptableObject
{
    public string setName = "新套装";

    // 关键：这一套衣服包含的所有单件（如：头、身、腿）
    public List<SkinItemSO> items;
}