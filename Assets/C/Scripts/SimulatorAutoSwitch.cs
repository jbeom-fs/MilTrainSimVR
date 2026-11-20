using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace MilTrainSimVR.UrbanCombat
{
    // 헤드셋이 연결돼 있으면 XR Interaction Simulator 를 켜지 않는다.
    // 시뮬레이터는 켜지는 순간 진짜 헤드셋 입력 장치를 지운다(removeOtherHMDDevices 기본값 true).
    // 그래서 시뮬레이터 오브젝트는 씬에 '꺼진 채로' 두고, 헤드셋이 없을 때만 여기서 켠다.
    // OpenXR 은 런타임이 '준비됨'을 알려 준 뒤에야 디스플레이를 켜서 씬 시작보다 몇 프레임 늦을 수 있다
    // (10/22 Virtual Desktop 실기에서 Awake 때는 아직 꺼져 있었다) → XR 이 로드됐으면 잠깐 기다려 보고 정한다.
    public class SimulatorAutoSwitch : MonoBehaviour
    {
        [Tooltip("씬에 꺼진 채로 둔 XR Interaction Simulator 오브젝트")]
        [SerializeField] GameObject m_Simulator;

        [Tooltip("XR 이 로드됐을 때 헤드셋 화면이 켜지기를 기다리는 최대 시간(초)")]
        [SerializeField] float m_WaitSeconds = 5f;

        IEnumerator Start()
        {
            // XR 로더가 없으면(헤드셋 없이 시작) 기다릴 것 없이 바로 시뮬레이터를 켠다
            float deadline = Time.realtimeSinceStartup + (IsXRLoaded() ? m_WaitSeconds : 0f);
            while (!IsHeadsetRunning() && Time.realtimeSinceStartup < deadline)
                yield return null;

            bool headset = IsHeadsetRunning();
            if (m_Simulator != null)
                m_Simulator.SetActive(!headset);
            Debug.Log(headset ? "[C] 헤드셋 감지 — 시뮬레이터 끔" : "[C] 헤드셋 없음 — 시뮬레이터 켬 (키보드·마우스)");
        }

        static bool IsXRLoaded()
        {
            XRGeneralSettings settings = XRGeneralSettings.Instance;
            return settings != null && settings.Manager != null && settings.Manager.activeLoader != null;
        }

        static bool IsHeadsetRunning()
        {
            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            foreach (XRDisplaySubsystem display in displays)
            {
                if (display.running)
                    return true;
            }
            return false;
        }
    }
}
