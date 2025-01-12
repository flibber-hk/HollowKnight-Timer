using System.Reflection;
using Modding;
using UnityEngine.SceneManagement;
using USceneManager = UnityEngine.SceneManagement.SceneManager;
using UnityEngine;
using Modding.Menu;
using Modding.Menu.Config;
using UnityEngine.UI;
using System;

namespace HKTimer {
    public class HKTimer : Mod, ITogglableMod, ICustomMenuMod, IGlobalSettings<Settings> {
        public static Settings settings { get; set; } = new Settings();

        public void OnLoadGlobal(Settings s) => settings = s;
        public Settings OnSaveGlobal() => settings;

        public void OnLoadLocal(double s) => Log($"Loaded {s}");
        public double OnSaveLocal() {
            if(triggerManager != null) return triggerManager.pb.TotalSeconds;
            else return 0;
        }

        public static HKTimer instance { get; private set; }

        public GameObject gameObject { get; private set; }
        public Timer timer { get; private set; }
        public TriggerManager triggerManager { get; private set; }

        public bool ToggleButtonInsideMenu => true;

        internal MenuScreen screen;

        public override string GetVersion() => Assembly.GetExecutingAssembly().GetName().Version.ToString();

        public override void Initialize() {
            if(instance != null) {
                return;
            }
            instance = this;
            gameObject = new GameObject();

            timer = gameObject.AddComponent<Timer>();
            timer.InitDisplay();
            timer.ShowDisplay(settings.showTimer);
            gameObject.AddComponent<MenuKeybindListener>();
            triggerManager = gameObject.AddComponent<TriggerManager>().Initialize(timer);
            triggerManager.InitDisplay();
            if(System.Enum.TryParse<TriggerManager.TriggerPlaceType>(settings.trigger, out var t)) {
                triggerManager.triggerPlaceType = t;
            } else {
                LogError($"Invalid trigger name {settings.trigger}");
            }

            USceneManager.activeSceneChanged += SceneChanged;
            GameObject.DontDestroyOnLoad(gameObject);
        }

        public void Unload() {
            this.timer.UnloadHooks();
            GameObject.DestroyImmediate(gameObject);
            USceneManager.activeSceneChanged -= SceneChanged;
            HKTimer.instance = null;
        }

        private void SceneChanged(Scene from, Scene to) {
            triggerManager.SpawnTriggers();
        }

        public MenuScreen GetMenuScreen(MenuScreen modListMenu, ModToggleDelegates? toggleDelegates) {
            // this should always work
            var dels = toggleDelegates.Value;
            Action<MenuSelectable> cancelAction = _ => {
                dels.ApplyChange();
                UIManager.instance.UIGoToDynamicMenu(modListMenu);
            };
            MappableKey setStartKeybind = null;
            MappableKey setEndKeybind = null;
            this.screen = new MenuBuilder(UIManager.instance.UICanvas.gameObject, "HKTimerMenu")
                .CreateTitle("HKTimer", MenuTitleStyle.vanillaStyle)
                .CreateContentPane(RectTransformData.FromSizeAndPos(
                    new RelVector2(new Vector2(1920f, 903f)),
                    new AnchoredPosition(
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0f, -60f)
                    )
                ))
                .CreateControlPane(RectTransformData.FromSizeAndPos(
                    new RelVector2(new Vector2(1920f, 259f)),
                    new AnchoredPosition(
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0f, -502f)
                    )
                ))
                .SetDefaultNavGraph(new GridNavGraph(1))
                .AddContent(
                    RegularGridLayout.CreateVerticalLayout(105f),
                    c => {
                        c.AddHorizontalOption(
                            "ToggleModOption",
                            new HorizontalOptionConfig {
                                Label = "Mod Enabled",
                                Options = new string[] { "Off", "On" },
                                ApplySetting = (_, i) => dels.SetModEnabled(i == 1),
                                RefreshSetting = (s, _) => s.optionList.SetOptionTo(dels.GetModEnabled() ? 1 : 0),
                                CancelAction = cancelAction
                            },
                            out var toggleModOption
                        ).AddHorizontalOption(
                            "ShowTimerOption",
                            new HorizontalOptionConfig {
                                Label = "Show Timer",
                                Options = new string[] { "Off", "On" },
                                ApplySetting = (_, i) => {
                                    settings.showTimer = i == 1;
                                    if(HKTimer.instance != null) {
                                        HKTimer.instance.timer.ShowDisplay(settings.showTimer);
                                    }
                                },
                                RefreshSetting = (s, _) => s.optionList.SetOptionTo(settings.showTimer ? 1 : 0),
                                CancelAction = cancelAction,
                                Style = HorizontalOptionStyle.VanillaStyle
                            },
                            out var showTimerOption
                        ).AddMenuButton(
                            "ResetBestButton",
                            new MenuButtonConfig {
                                Label = "Reset Personal Best",
                                SubmitAction = _ => {
                                    if(HKTimer.instance != null) HKTimer.instance.triggerManager.ResetPB();
                                },
                                CancelAction = cancelAction,
                                Style = MenuButtonStyle.VanillaStyle
                            }
                        ).AddHorizontalOption(
                            "TriggerTypeOption",
                            new HorizontalOptionConfig {
                                Label = "Trigger Type",
                                Options = new string[] { "Collision", "Movement", "Scene" },
                                CancelAction = cancelAction,
                                ApplySetting = (_, i) => {
                                    var trigger = i switch {
                                        0 => TriggerManager.TriggerPlaceType.Collision,
                                        1 => TriggerManager.TriggerPlaceType.Movement,
                                        2 => TriggerManager.TriggerPlaceType.Scene,
                                        _ => default // shouldn't ever happen
                                    };
                                    if(HKTimer.instance != null) {
                                        HKTimer.instance.triggerManager.triggerPlaceType = trigger;
                                    }
                                    settings.trigger = trigger.ToString();
                                },
                                RefreshSetting = (s, _) => {
                                    if(System.Enum.TryParse(settings.trigger, out TriggerManager.TriggerPlaceType t)) {
                                        s.optionList.SetOptionTo((int) t);
                                    }
                                },
                                Description = new DescriptionInfo {
                                    Text = "The trigger type to place."
                                }
                            },
                            out var triggerTypeOption
                        ).AddMenuButton(
                            "LoadTriggersButton",
                            new MenuButtonConfig {
                                Label = "Load Triggers",
                                SubmitAction = _ => {
                                    if(HKTimer.instance != null) HKTimer.instance.triggerManager.LoadTriggers();
                                },
                                CancelAction = cancelAction,
                                Style = MenuButtonStyle.VanillaStyle
                            }
                        ).AddMenuButton(
                            "SaveTriggersButton",
                            new MenuButtonConfig {
                                Label = "Save Triggers",
                                SubmitAction = _ => {
                                    if(HKTimer.instance != null) HKTimer.instance.triggerManager.SaveTriggers();
                                },
                                CancelAction = cancelAction,
                                Style = MenuButtonStyle.VanillaStyle
                            },
                            out var saveTriggersButton
                        );
                        // should be guaranteed from `MenuBuilder.AddContent`
                        if(c.Layout is RegularGridLayout layout) {
                            var l = layout.ItemAdvance;
                            l.x = new RelLength(750f);
                            layout.ChangeColumns(2, 0.5f, l, 0.5f);
                        }
                        GridNavGraph navGraph = c.NavGraph as GridNavGraph;
                        navGraph.ChangeColumns(2);
                        c.AddKeybind(
                            "PauseKeybind",
                            settings.keybinds.pause,
                            new KeybindConfig {
                                Label = "Pause",
                                CancelAction = cancelAction
                            },
                            out var pauseKeybind
                        ).AddKeybind(
                            "ResetKeybind",
                            settings.keybinds.reset,
                            new KeybindConfig {
                                Label = "Reset",
                                CancelAction = cancelAction
                            }
                        ).AddKeybind(
                            "SetStartKeybind",
                            settings.keybinds.setStart,
                            new KeybindConfig {
                                Label = "Set Start",
                                CancelAction = cancelAction
                            },
                            out setStartKeybind
                        ).AddKeybind(
                            "SetEndKeybind",
                            settings.keybinds.setEnd,
                            new KeybindConfig {
                                Label = "Set End",
                                CancelAction = cancelAction
                            },
                            out setEndKeybind
                        );
                        navGraph.ChangeColumns(2);
                        toggleModOption.GetComponent<MenuSetting>().RefreshValueFromGameSettings();
                        showTimerOption.GetComponent<MenuSetting>().RefreshValueFromGameSettings();
                        triggerTypeOption.GetComponent<MenuSetting>().RefreshValueFromGameSettings();
                    }
                )
                .AddControls(
                    new SingleContentLayout(new AnchoredPosition(
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0f, -64f)
                    )),
                    c => c.AddMenuButton(
                        "BackButton",
                        new MenuButtonConfig {
                            Label = "Back",
                            CancelAction = cancelAction,
                            SubmitAction = cancelAction,
                            Style = MenuButtonStyle.VanillaStyle,
                            Proceed = true
                        },
                        out var backButton
                    )
                )
                .Build();
            return this.screen;
        }
    }

    public class MenuKeybindListener : MonoBehaviour {
        public void Update() {
            if(HKTimer.settings.keybinds.openMenu.WasPressed) {
                if(GameManager.instance != null) {
                    this.StartCoroutine(GameManager.instance.PauseToggleDynamicMenu(HKTimer.instance.screen));
                }
            }
        }
    }
}