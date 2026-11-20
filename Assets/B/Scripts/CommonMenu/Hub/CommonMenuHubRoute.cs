using MilTrainSimVR.A.Common;
using UnityEngine.SceneManagement;

namespace MilTrainSimVR.B.CommonMenu
{
    // Read-only reuse of A's routing contract. No content assembly reference is required by the menu core.
    public sealed class CommonMenuHubRoute : CommonMenuMainRoute
    {
        public override bool IsAtMain => SceneManager.GetActiveScene().path == CourseRoute.HubScenePath;
        public override bool CanGoMain => CourseRoute.CanLoad(CourseRoute.HubScenePath);
        public override void GoMain() => CourseRoute.ReturnToHub();
    }
}
