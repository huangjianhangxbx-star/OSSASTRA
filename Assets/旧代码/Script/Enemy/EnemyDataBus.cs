using Com.LuisPedroFonseca.ProCamera2D.TopDownShooter;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyDataBus
{
    public string enemyName;

    public BattleCamp battleCamp = BattleCamp.EnemySide;

    public bool isAlive = true;

    public bool canBeTargeted = true;

    public float currentHp = 100f;

    public float maxHp = 100f;

    public EnemyRank enemyRank = EnemyRank.Minion;

    public void LoadFromSO(EnemySO so)
    {
        if (so == null)
            return;

        enemyName = so.enemyName;
        battleCamp = so.battleCamp;
        maxHp = so.maxHp;
        currentHp = maxHp;
        enemyRank = so.enemyRank;

        isAlive = true;
        canBeTargeted = true;
    }
}