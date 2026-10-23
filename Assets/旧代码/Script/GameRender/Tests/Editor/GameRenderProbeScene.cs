#if UNITY_EDITOR
using System;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

internal sealed class GameRenderProbeScene : IDisposable
{
    private readonly Scene previous;
    private readonly Scene temporary;

    internal GameRenderProbeScene()
    {
        previous = SceneManager.GetActiveScene();
        temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(temporary);
        }
        catch
        {
            EditorSceneManager.CloseScene(temporary, true);
            throw;
        }
    }

    public void Dispose()
    {
        if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        if (temporary.IsValid()) EditorSceneManager.CloseScene(temporary, true);
    }
}
#endif
