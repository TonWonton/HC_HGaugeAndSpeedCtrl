using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using H;
using HarmonyLib;
using UnityEngine;

namespace HC_HGaugeAndSpeedCtrl
{
    [BepInProcess("HoneyCome")]
    [BepInPlugin(GUID, PluginName, PluginVersion)]
    public class HGaugeAndSpeedCtrl : BasePlugin
    {
        public const string PluginName = "HC_HGaugeAndSpeedCtrl";
        public const string GUID = "HC_HGaugeAndSpeedCtrl";
        public const string PluginVersion = "1.1.1";
        //Instances
        public static HGaugeAndSpeedCtrl hGaugeInstance;
        public static HGaugeCtrlComponent hGaugeComponent;
        //Climax together
        public static ConfigEntry<bool> ClimaxTFemale;
        public static ConfigEntry<bool> ClimaxTMale;
        //Gauge speeds and scaling
        public static ConfigEntry<bool> Speed;
        public static ConfigEntry<float> speedScale;
        public static ConfigEntry<float> gaugeSpeedMultiplierF;
        public static ConfigEntry<float> gaugeHitSpeedMultiplierF;
        public static ConfigEntry<float> gaugeSpeedMultiplierM;
        public static ConfigEntry<float> gaugeHitSpeedMultiplierM;
        public static ConfigEntry<bool> KeyO;
        //Loop speeds
        public static ConfigEntry<float> minLoopSpeedW;
        public static ConfigEntry<float> maxLoopSpeedW;
        public static ConfigEntry<float> minLoopSpeedS;
        public static ConfigEntry<float> maxLoopSpeedS;
        public static ConfigEntry<float> minLoopSpeedO;
        public static ConfigEntry<float> maxLoopSpeedO;

        public override void Load()
        {
            //Climax together
            ClimaxTFemale = Config.Bind("Climax", "Female climax together", true, "Climax Together have priority when girl cums");
            ClimaxTMale = Config.Bind("Climax", "Male auto climax", true, "Priority:\nBoth(together, inside, outside)\nMale solo(swallow, spit, outside)");
            //Gauge speeds and scaling
            Speed = Config.Bind("Gauge", "Toggle speed scaling", false, "Gauge increase will scale with speed if enabled\nThis is independant from animation speed\nSlowest loop speed = 50% on current gauge speed\nFastest loop speed = 150% of current gauge speed");
            speedScale = Config.Bind("Gauge", "Speed scaling multiplier", 1f, new ConfigDescription("How much speed affects the gauge", new AcceptableValueRange<float>(0.1f, 4f)));
            gaugeSpeedMultiplierF = Config.Bind("Gauge", "Female base gauge speed", 0.7f, new ConfigDescription("How much the female gauge increases", new AcceptableValueRange<float>(0f, 4f)));
            gaugeHitSpeedMultiplierF = Config.Bind("Gauge", "Female pleasure gauge speed", 1.5f, new ConfigDescription("How much the female gauge increases when pleasure", new AcceptableValueRange<float>(0f, 6f)));
            gaugeSpeedMultiplierM = Config.Bind("Gauge", "Male base gauge speed", 1f, new ConfigDescription("How much the male gauge increases", new AcceptableValueRange<float>(0f, 4f)));
            gaugeHitSpeedMultiplierM = Config.Bind("Gauge", "Male pleasure gauge speed", 1.1f, new ConfigDescription("How much the male gauge increases when pleasure", new AcceptableValueRange<float>(0f, 6f)));
            Speed.SettingChanged += (sender, args) => HGaugeCtrlComponent.SetGaugeSpeed();
            speedScale.SettingChanged += (sender, args) => HGaugeCtrlComponent.SetGaugeSpeed();
            gaugeSpeedMultiplierF.SettingChanged += (sender, args) => HGaugeCtrlComponent.SetGaugeSpeed();
            gaugeHitSpeedMultiplierF.SettingChanged += (sender, args) => HGaugeCtrlComponent.SetGaugeSpeed();
            gaugeSpeedMultiplierM.SettingChanged += (sender, args) => HGaugeCtrlComponent.SetGaugeSpeed();
            gaugeHitSpeedMultiplierM.SettingChanged += (sender, args) => HGaugeCtrlComponent.SetGaugeSpeed();
            //Loop speeds
            minLoopSpeedW = Config.Bind("Loop speed", "Minimum speed weak loop", 1f, new ConfigDescription("If minimum is higher than max speed\nmax speed becomes minimum vice versa", new AcceptableValueRange<float>(0.2f, 5.8f)));
            maxLoopSpeedW = Config.Bind("Loop speed", "Maximum speed weak loop", 1.6f, new ConfigDescription("If minimum is higher than max speed\nmax speed becomes minimum vice versa", new AcceptableValueRange<float>(1f, 7.8f)));
            minLoopSpeedS = Config.Bind("Loop speed", "Minimum speed strong loop", 1.4f, new ConfigDescription("If minimum is higher than max speed\nmax speed becomes minimum vice versa", new AcceptableValueRange<float>(0.1f, 4.6f)));
            maxLoopSpeedS = Config.Bind("Loop speed", "Maximum speed strong loop", 2f, new ConfigDescription("If minimum is higher than max speed\nmax speed becomes minimum vice versa", new AcceptableValueRange<float>(1f, 6.2f)));
            minLoopSpeedO = Config.Bind("Loop speed", "Minimum speed orgasm loop", 1.4f, new ConfigDescription("If minimum is higher than max speed\nmax speed becomes minimum vice versa", new AcceptableValueRange<float>(0.1f, 3.2f)));
            maxLoopSpeedO = Config.Bind("Loop speed", "Maximum speed orgasm loop", 2f, new ConfigDescription("If minimum is higher than max speed\nmax speed becomes minimum vice versa", new AcceptableValueRange<float>(1f, 4.4f)));
            KeyO = Config.Bind("Keybind", "Enable keybinds", true, "LeftCtrl key + Finish button : female only\n" +
                               "LeftShift key + Finish button : male only\n" +
                               "Right mouse double click : Change the strength of the motion(WLoop <=> SLoop)");
            minLoopSpeedW.SettingChanged += (sender, args) => HGaugeCtrlComponent.ApplyLoopSpeeds();
            maxLoopSpeedW.SettingChanged += (sender, args) => HGaugeCtrlComponent.ApplyLoopSpeeds();
            minLoopSpeedS.SettingChanged += (sender, args) => HGaugeCtrlComponent.ApplyLoopSpeeds();
            maxLoopSpeedS.SettingChanged += (sender, args) => HGaugeCtrlComponent.ApplyLoopSpeeds();
            minLoopSpeedO.SettingChanged += (sender, args) => HGaugeCtrlComponent.ApplyLoopSpeeds();
            maxLoopSpeedO.SettingChanged += (sender, args) => HGaugeCtrlComponent.ApplyLoopSpeeds();
            //Set instance and patch
            hGaugeInstance = this;
            Harmony.CreateAndPatchAll(typeof(HGaugeCtrlComponent.Hooks), GUID);
        }

        public class HGaugeCtrlComponent : MonoBehaviour
        {
            //Instances
            private HScene hScene;
            private HSceneSprite hSceneSprite;
            private HScene.AnimationListInfo _info;
            //System
            private bool pausedEnabledProc;
            private float paused;
            private float _isDoubleClick;
            private bool clickChangeSpeed;
            //Gauge speeds
            private static float gaugeIncreaseF;
            private static float gaugeHitIncreaseF;
            private static float gaugeIncreaseM;
            private static float gaugeHitIncreaseM;
            //Feel animation and procs
            private bool fFeelAnimation;
            private bool fFeelAnimationProc;
            private bool mFeelAnimation;
            private bool mFeelAnimationProc;
            private bool isMasturbation;
            private bool flag;
            private string _playAnimation;
            private bool maleFinishing;
            private bool[] buttonList;
            private bool pausedEnabledShouldProc() {
                return paused == 0f && HGaugeAndSpeedCtrl.Speed.Value;
            }
            private bool fFeelAnimationShouldProc() {
                return fFeelAnimation && flag && !hScene.CtrlFlag.StopFeelFemale;
            }
            private bool mFeelAnimationShouldProc() {
                return mFeelAnimation && flag && !hScene.CtrlFlag.StopFeelMale;
            }
            private bool fFeelAnimationF2M1ShouldProc(string animationName) {
                switch (animationName)
                {
                    case "2x Blowjob":
                        return false;
                    case "2x Blowjob (Alt)":
                        return false;
                    case "2x HJ Lick":
                        return false;
                    case "2x HJ Lick (Alt)":
                        return false;
                    default: return true;
                }
            }

            void Update()
            {
                if (Input.GetMouseButtonDown(1) && _isDoubleClick <= 0f) _isDoubleClick = 0.4f; //Set double click timer
                else if (Input.GetMouseButtonDown(1) && _isDoubleClick > 0f) //Check for new click while timer active
                {
                    clickChangeSpeed = true;
                    _isDoubleClick = 0f;
                }
                else if (_isDoubleClick > 0f) _isDoubleClick -= Time.deltaTime; //If no click decrease timer

                //If speed scaling enabled and game not paused
                if (pausedEnabledProc)
                {
                    if (fFeelAnimationProc)
                    {
                        if (isMasturbation)
                        {   //If masturbation
                            if (!hScene.CtrlFlag.IsGaugeHit)
                                hScene.CtrlFlag.Feel_f += Time.deltaTime * gaugeIncreaseF * (hScene.CtrlFlag.Speed + 0.5f);
                            else hScene.CtrlFlag.Feel_f += Time.deltaTime * gaugeHitIncreaseF * (hScene.CtrlFlag.Speed + 0.5f);
                        }
                        else if (!hScene.CtrlFlag.IsGaugeHit)
                        {   //If normal female gauge
                            if (hScene.CtrlFlag.LoopType == 1)
                                hScene.CtrlFlag.Feel_f += Time.deltaTime * gaugeIncreaseF * (hScene.CtrlFlag.Speed - 0.5f);
                            else hScene.CtrlFlag.Feel_f += Time.deltaTime * gaugeIncreaseF * (hScene.CtrlFlag.Speed + 0.5f);
                        }
                        else
                        {  //If female gauge hit
                            if (hScene.CtrlFlag.LoopType == 1)
                                hScene.CtrlFlag.Feel_f += Time.deltaTime * gaugeHitIncreaseF * (hScene.CtrlFlag.Speed - 0.5f);
                            else hScene.CtrlFlag.Feel_f += Time.deltaTime * gaugeHitIncreaseF * (hScene.CtrlFlag.Speed + 0.5f);
                        }
                    }
                    if (mFeelAnimationProc)
                    {
                        if (!hScene.CtrlFlag.IsGaugeHit_M)
                        {   //If normal male gauge
                            if (hScene.CtrlFlag.LoopType == 1)
                                hScene.CtrlFlag.Feel_m += Time.deltaTime * gaugeIncreaseM * (hScene.CtrlFlag.Speed - 0.5f);
                            else hScene.CtrlFlag.Feel_m += Time.deltaTime * gaugeIncreaseM * (hScene.CtrlFlag.Speed + 0.5f);
                        }
                        else
                        {  //If male gauge hit
                            if (hScene.CtrlFlag.LoopType == 1)
                                hScene.CtrlFlag.Feel_m += Time.deltaTime * gaugeHitIncreaseM * (hScene.CtrlFlag.Speed - 0.5f);
                            else hScene.CtrlFlag.Feel_m += Time.deltaTime * gaugeHitIncreaseM * (hScene.CtrlFlag.Speed + 0.5f);
                        }
                    }
                }
                //If speed scaling disabled
                else if (paused == 0f)
                {
                    if (fFeelAnimationProc)
                    {
                        if (isMasturbation)
                        {   //If masturbation
                            if (!hScene.CtrlFlag.IsGaugeHit)
                                hScene.CtrlFlag.Feel_f += Time.deltaTime * gaugeIncreaseF;
                            else hScene.CtrlFlag.Feel_f += Time.deltaTime * gaugeHitIncreaseF;
                        }
                        else if (!hScene.CtrlFlag.IsGaugeHit)
                        {   //If normal female gauge
                            if (hScene.CtrlFlag.LoopType == 1)
                                hScene.CtrlFlag.Feel_f += Time.deltaTime * gaugeIncreaseF;
                            else hScene.CtrlFlag.Feel_f += Time.deltaTime * gaugeIncreaseF;
                        }
                        else
                        {  //If female gauge hit
                            if (hScene.CtrlFlag.LoopType == 1)
                                hScene.CtrlFlag.Feel_f += Time.deltaTime * gaugeHitIncreaseF;
                            else hScene.CtrlFlag.Feel_f += Time.deltaTime * gaugeHitIncreaseF;
                        }
                    }
                    if (mFeelAnimationProc)
                    {
                        if (!hScene.CtrlFlag.IsGaugeHit_M)
                        {   //If normal male gauge
                            if (hScene.CtrlFlag.LoopType == 1)
                                hScene.CtrlFlag.Feel_m += Time.deltaTime * gaugeIncreaseM;
                            else hScene.CtrlFlag.Feel_m += Time.deltaTime * gaugeIncreaseM;
                        }
                        else
                        { //If male gauge hit
                            if (hScene.CtrlFlag.LoopType == 1)
                                hScene.CtrlFlag.Feel_m += Time.deltaTime * gaugeHitIncreaseM;
                            else hScene.CtrlFlag.Feel_m += Time.deltaTime * gaugeHitIncreaseM;
                        }
                    }
                }
            }
            
            void LateUpdate()
            {
                if (clickChangeSpeed == true)
                    clickChangeSpeed = false;
            }

            public static void SetGaugeSpeed()
            {
                if (Speed.Value)
                {
                    //If speed scaling enabled
                    gaugeIncreaseF = 0.03f * speedScale.Value * gaugeSpeedMultiplierF.Value;
                    gaugeHitIncreaseF = 0.03f * speedScale.Value * gaugeHitSpeedMultiplierF.Value;
                    gaugeIncreaseM = 0.03f * speedScale.Value * gaugeSpeedMultiplierM.Value;
                    gaugeHitIncreaseM = 0.03f * speedScale.Value * gaugeHitSpeedMultiplierM.Value;
                }
                else
                {
                    //If speed scaling disabled
                    gaugeIncreaseF = 0.03f * gaugeSpeedMultiplierF.Value;
                    gaugeHitIncreaseF = 0.03f * gaugeHitSpeedMultiplierF.Value;
                    gaugeIncreaseM = 0.03f * gaugeSpeedMultiplierM.Value;
                    gaugeHitIncreaseM = 0.03f * gaugeHitSpeedMultiplierM.Value;
                }
                if (hGaugeComponent != null)
                    hGaugeComponent.pausedEnabledProc = hGaugeComponent.pausedEnabledShouldProc();
            }

            public static void ApplyLoopSpeeds()
            {
                //Apply custom loop speeds
                if (hGaugeComponent != null)
                    hGaugeComponent.hScene.CtrlFlag.LoopSpeeds = new HSceneFlagCtrl.LoopSpeed()
                    {
                        MinLoopSpeedW = minLoopSpeedW.Value,
                        MaxLoopSpeedW = maxLoopSpeedW.Value,
                        MinLoopSpeedS = minLoopSpeedS.Value,
                        MaxLoopSpeedS = maxLoopSpeedS.Value,
                        MinLoopSpeedO = minLoopSpeedO.Value,
                        MaxLoopSpeedO = maxLoopSpeedO.Value
                    };
            }

            private void CheckPositionFeel(HScene.AnimationListInfo info)
            {
                isMasturbation = false;
                switch (hScene._mode)
                {
                    case 2: //Sonyu
                        {
                        fFeelAnimation = true;
                        if (hScene.CtrlFlag.IsFaintness)
                        {
                            switch (info.NameAnimation)
                            {
                                case "Sitting 69":
                                    mFeelAnimation = false;
                                        break;
                                case "69":
                                    mFeelAnimation = false;
                                    break;
                                case "BJ Masturbation":
                                     mFeelAnimation = false;
                                      break;
                                case "Mutual Caress":
                                    mFeelAnimation = false;
                                    break;
                                case "Cunnilingus HJ":
                                    mFeelAnimation = false;
                                    break;
                                default:
                                    mFeelAnimation = true;
                                    break;
                            }
                        }
                        else mFeelAnimation = true;
                        break;
                    }
                    case 6: //Les
                        fFeelAnimation = true;
                        mFeelAnimation = false;
                        break;
                    case 0: //Aibu
                        fFeelAnimation = true;
                        mFeelAnimation = false;
                        break;
                    case 1: //Houshi
                        fFeelAnimation = false;
                        mFeelAnimation = true;
                        break;
                    case 7: //F2M1
                        fFeelAnimation = fFeelAnimationF2M1ShouldProc(info.NameAnimation);
                        mFeelAnimation = true;
                        break;
                    case 8: //F1M2
                        if (info.NameAnimation == "Double BJ")
                            fFeelAnimation = false;
                        else fFeelAnimation = true;
                        mFeelAnimation = true;
                        break;
                    case 4: //Masturbation
                        fFeelAnimation = true;
                        mFeelAnimation = false;
                        isMasturbation = true;
                        break;
                    case 3: //Spanking
                        fFeelAnimation = true;
                        mFeelAnimation = false;
                        break;
                }
            }

            public static class Hooks
            {
                [HarmonyPostfix]
                [HarmonyPatch(typeof(HScene), "Start")]
                public static void StartHook(HScene __instance)
                {
                    //Add component and get instances
                    hGaugeComponent = hGaugeInstance.AddComponent<HGaugeCtrlComponent>();
                    hGaugeComponent.hScene = __instance;
                    hGaugeComponent.hSceneSprite = hGaugeComponent.hScene._sprite;
                    //Calculate and set gauge increase rates
                    hGaugeComponent.hScene.CtrlFlag.SpeedGuageRate = 0f;
                    HGaugeCtrlComponent.SetGaugeSpeed();
                    hGaugeComponent.pausedEnabledProc = hGaugeComponent.pausedEnabledShouldProc();
                }

                [HarmonyPostfix]
                [HarmonyPatch(typeof(HSceneFlagCtrl), "Start")]
                public static void HSceneFlagCtrlStartHook()
                {
                    HGaugeCtrlComponent.ApplyLoopSpeeds();
                }

                [HarmonyPostfix]
                [HarmonyPatch(typeof(Sonyu), "SetPlay")]
                [HarmonyPatch(typeof(Les), "SetPlay")]
                [HarmonyPatch(typeof(Aibu), "setPlay")]
                [HarmonyPatch(typeof(MultiPlay_F2M1), "setPlay")]
                [HarmonyPatch(typeof(MultiPlay_F1M2), "SetPlay")]
                [HarmonyPatch(typeof(Masturbation), "SetPlay")]
                [HarmonyPatch(typeof(Houshi), "SetPlay")]
                public static void SetPlayHook(string playAnimation)
                {
                    hGaugeComponent._playAnimation = playAnimation;
                    hGaugeComponent.flag = (hGaugeComponent._playAnimation == "WLoop" || hGaugeComponent._playAnimation == "D_WLoop" ||
                                            hGaugeComponent._playAnimation == "MLoop" || hGaugeComponent._playAnimation == "D_MLoop" ||
                                            hGaugeComponent._playAnimation == "OLoop" || hGaugeComponent._playAnimation == "D_OLoop" ||
                                            hGaugeComponent._playAnimation == "SLoop" || hGaugeComponent._playAnimation == "D_SLoop");
                    hGaugeComponent.CheckPositionFeel(hGaugeComponent._info);
                    hGaugeComponent.fFeelAnimationProc = hGaugeComponent.fFeelAnimationShouldProc();
                    hGaugeComponent.mFeelAnimationProc = hGaugeComponent.mFeelAnimationShouldProc();
                }

                [HarmonyPostfix]
                [HarmonyPatch(typeof(HScene), "ChangeModeCtrl")]
                public static void ChangeModeCtrlHook(HScene.AnimationListInfo info)
                {
                    hGaugeComponent._info = info;
                }

                [HarmonyPrefix]
                [HarmonyPatch(typeof(HScene), "Update")]
                public static void PreUpdateHook()
                {
                    //If female can climax together
                    if (hGaugeComponent.hScene.CtrlFlag.Feel_f >= 0.99f && HGaugeAndSpeedCtrl.ClimaxTFemale.Value && hGaugeComponent.hSceneSprite.CategoryFinish.GetActiveButton()[5])
                    {
                        hGaugeComponent.hSceneSprite.OnClickFinishSame();
                        hGaugeComponent.maleFinishing = true;
                    }
                    //If male can climax
                    else if (hGaugeComponent.hScene.CtrlFlag.Feel_m >= 0.99f && HGaugeAndSpeedCtrl.ClimaxTMale.Value)
                    {
                        //Together
                        hGaugeComponent.buttonList = hGaugeComponent.hSceneSprite.CategoryFinish.GetActiveButton();
                        if (hGaugeComponent.hSceneSprite.CategoryFinish._houshiPosKind == 0)
                        {
                            if (hGaugeComponent.buttonList[5])
                                hGaugeComponent.hSceneSprite.OnClickFinishSame();
                            else if (hGaugeComponent.buttonList[2])
                                hGaugeComponent.hSceneSprite.OnClickFinishInSide();
                            else if (hGaugeComponent.buttonList[1])
                                hGaugeComponent.hSceneSprite.OnClickFinishOutSide();
                        }
                        //Alone
                        else if (hGaugeComponent.hSceneSprite.CategoryFinish._houshiPosKind == 1)
                        {
                            if (hGaugeComponent.buttonList[3])
                                hGaugeComponent.hSceneSprite.OnClickFinishDrink();
                            else if (hGaugeComponent.buttonList[4])
                                hGaugeComponent.hSceneSprite.OnClickFinishVomit();
                            else if (hGaugeComponent.buttonList[1])
                                hGaugeComponent.hSceneSprite.OnClickFinishOutSide();
                        }
                        hGaugeComponent.maleFinishing = true;
                    }
                }

                [HarmonyPrefix]
                [HarmonyPatch(typeof(HScene), "LateUpdate")]
                public static void LateUpdateHook()
                {
                    //If male is finishing stop gauge hit and reset gauge
                    if (hGaugeComponent.hScene.CtrlFlag.NowOrgasm == true && hGaugeComponent.maleFinishing == true)
                    {
                        hGaugeComponent.hScene.CtrlFlag.IsGaugeHit = false;
                        hGaugeComponent.hScene.CtrlFlag.IsGaugeHit_M = false;
                        hGaugeComponent.hScene.CtrlFlag.Feel_m = 0f;
                    }
                    //Reset bool when male orgasm is over
                    else if (hGaugeComponent.hScene.CtrlFlag.NowOrgasm == false && hGaugeComponent.maleFinishing == true)
                        hGaugeComponent.maleFinishing = false;
                }

                [HarmonyPrefix]
                [HarmonyPatch(typeof(Sonyu), "SetAnimationParamater")]
                [HarmonyPatch(typeof(Aibu), "SetAnimationParamater")]
                [HarmonyPatch(typeof(Houshi), "SetAnimationParamater")]
                [HarmonyPatch(typeof(MultiPlay_F1M2), "SetAnimationParamater")]
                [HarmonyPatch(typeof(MultiPlay_F2M1), "SetAnimationParamater")]
                [HarmonyPatch(typeof(Les), "SetAnimationParamater")]
                [HarmonyPatch(typeof(Masturbation), "SetAnimationParamater")]
                public static void SetAnimationParamaterHook()
                {
                    if (hGaugeComponent.clickChangeSpeed == true)
                        if (HGaugeAndSpeedCtrl.KeyO.Value)
                            switch (hGaugeComponent.hScene.CtrlFlag.LoopType)
                            {
                                case 0:
                                    hGaugeComponent.hScene.CtrlFlag.Speed += 1.001f;
                                    break;
                                case 1:
                                    hGaugeComponent.hScene.CtrlFlag.Speed -= 1.001f;
                                    break;
                                case 2:
                                    if (hGaugeComponent.hScene.CtrlFlag.Speed > 0.5f)
                                        hGaugeComponent.hScene.CtrlFlag.Speed = 0f;
                                    else
                                        hGaugeComponent.hScene.CtrlFlag.Speed = 1f;
                                    break;
                            }
                }

                [HarmonyPostfix]
                [HarmonyPatch(typeof(HSceneSprite), "OnClickStopFeel")]
                public static void OnClickStopFeelHook()
                {
                    hGaugeComponent.fFeelAnimationProc = hGaugeComponent.fFeelAnimationShouldProc();
                    hGaugeComponent.mFeelAnimationProc = hGaugeComponent.mFeelAnimationShouldProc();
                }

                [HarmonyPrefix]
                [HarmonyPatch(typeof(HSceneSprite), "OnClickFinishBefore")]
                public static bool OnClickFinishBeforeHook()
                {
                    if (HGaugeAndSpeedCtrl.KeyO.Value)
                        if (Input.GetKey(KeyCode.LeftShift))
                        {
                            if (hGaugeComponent.hScene.CtrlFlag.Feel_m < 0.75 && hGaugeComponent.mFeelAnimation)
                                hGaugeComponent.hScene.CtrlFlag.Feel_m = 0.75f;
                            return false;
                        }
                        if (Input.GetKey(KeyCode.LeftControl))
                        {
                            if (hGaugeComponent.hScene.CtrlFlag.Feel_f < 0.75 && hGaugeComponent.fFeelAnimation)
                                hGaugeComponent.hScene.CtrlFlag.Feel_f = 0.75f;
                            return false;
                        }
                    return true;
                }

                [HarmonyPrefix]
                [HarmonyPatch(typeof(HC.Dialog.ShortcutViewDialog), "Load")]
                [HarmonyPatch(typeof(HC.Dialog.HelpWindow), "Load")]
                [HarmonyPatch(typeof(HC.Dialog.ExitDialog), "Manager_Scene_IOverlap_AddEvent")]
                [HarmonyPatch(typeof(HC.Config.ConfigWindow), "Load")]
                public static void ConfigSHook()
                {
                    if (hGaugeComponent != null)
                    {
                        hGaugeComponent.paused++;
                        hGaugeComponent.pausedEnabledProc = hGaugeComponent.pausedEnabledShouldProc();
                    }
                }
                [HarmonyPrefix]
                [HarmonyPatch(typeof(HC.Dialog.ShortcutViewDialog), "OnBack")]
                [HarmonyPatch(typeof(HC.Dialog.HelpWindow), "SceneEnd")]
                [HarmonyPatch(typeof(HC.Dialog.ExitDialog), "Manager_Scene_IOverlap_RemoveEvent")]
                [HarmonyPatch(typeof(HC.Config.ConfigWindow), "Unload")]
                public static void ConfigEHook()
                {
                    if (hGaugeComponent != null)
                    {
                        hGaugeComponent.paused--;
                        hGaugeComponent.pausedEnabledProc = hGaugeComponent.pausedEnabledShouldProc();
                    }
                }

                [HarmonyPrefix]
                [HarmonyPatch(typeof(HScene), "OnDestroy")]
                public static void HScenePreOnDestroy()
                {
                    //Reset variables by destroying component
                    Destroy(HGaugeAndSpeedCtrl.hGaugeComponent);
                }
            }
        }
    }
}
