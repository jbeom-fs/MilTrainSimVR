using UnityEngine;

namespace MilTrainSimVR.B.CommonMenu
{
    // How "메인으로" reaches the main screen. The menu core knows no scene paths; the hub adapter (CommonMenuHubRoute, assembly
    // MilTrainSimVR.B.CommonMenu.Hub) uses A's CourseRoute. Without a route the button stays disabled.
    public abstract class CommonMenuMainRoute : MonoBehaviour
    {
        // The menu is in the main scene itself: the button shows but is disabled.
        public abstract bool IsAtMain { get; }
        // The main scene can be loaded now. False: the menu stays open with a short notice.
        public abstract bool CanGoMain { get; }
        // Starts loading the main scene. An exception keeps the menu open with the notice.
        public abstract void GoMain();
    }
}
