namespace MilTrainSimVR.B.CommonMenu
{
    // Hook for any scene content that must stop while the common pause menu holds the game (training, AI, shooting, timers,
    // portals). Implement it on an enabled MonoBehaviour in a loaded scene: the menu finds the listeners each time it opens.
    // Code that is not a MonoBehaviour can use CommonPauseMenu.Paused / Resumed / Leaving or poll CommonPauseMenu.IsPaused.
    //
    // Order. Paused: after Time.timeScale is 0 and AudioListener.pause is true, before the panel shows. Resumed: once per Paused,
    // after the user's buttons and sticks were seen released and the menu's input block is gone, before the time scale and the
    // audio pause come back. Leaving: "메인으로" or "게임종료" was chosen; no Resumed follows (the scene is about to unload or the
    // application quits), so stay stopped. If the hub cannot load after Leaving, the menu opens again without a second Paused and
    // a later close sends Resumed as usual.
    // Each call is guarded: an exception is logged and the menu goes on.
    public interface ICommonMenuListener
    {
        void OnCommonMenuPaused();
        void OnCommonMenuResumed();
        void OnCommonMenuLeaving();
    }
}
