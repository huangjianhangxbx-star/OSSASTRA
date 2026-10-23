using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HuntFlowDataBus
{
    public CharacterRoot player;
    public bool isRunActive;
    public bool isPaused;
    public bool isGameOver;

    public float elapsedTime;
    public float runDuration;

    public int killCount;
    public int currentLevel;
    public int currentExp;
    public int expToNextLevel;

    public List<CharacterRoot> aliveEnemies = new();
    public List<CharacterRoot> aliveAllies = new();

    //public RunConfigSO runConfig;
    //public MapConfigSO mapConfig;
    //public EnemyWaveConfigSO waveConfig;

    public void Reset()
    {
        isRunActive = false;
        isPaused = false;
        isGameOver = false;

        elapsedTime = 0f;
        killCount = 0;
        currentLevel = 1;
        currentExp = 0;

        player = null;
        aliveEnemies.Clear();
        aliveAllies.Clear();
    }
}
