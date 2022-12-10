using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Configuration;
using System.IO;
using System.Reflection;
using System;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ServerSync;
using fastJSON;
using System.Threading.Tasks;
using System.Collections;
using System.Linq;
using UnityEngine.Video;

#pragma warning disable CS0618
namespace FinalCredits
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class Plugin : BaseUnityPlugin
    {
        #region values
        private const string ModName = "FinalCredits", ModVersion = "0.1.0", ModGUID = "com.Frogger." + ModName;
        private static Harmony harmony = new(ModGUID);
        public static Plugin _self;
        private AssetBundle _embeddedResourceBundle;

        private GameObject creditsGameObject;
        private RectTransform creditsContactInfo;
        private Image creditsBGImage;
        private GameObject creditsParrentGameObject;
        private Button creditsCloseButton;
        private RawImage creditsVideoImage;
        private VideoPlayer creditsVideoPlayer;
        private CustomCredits customCredits = new();
        private CustomCreditsData.UnityBlock unityBlockPrefab;

        public static Font defaultFond;
        public static Font norseboldFond;
        #endregion
        #region ConfigSettings
        static string ConfigFileName = "com.Frogger.FinalCredits.cfg";
        DateTime LastConfigChange;
        public static readonly ConfigSync configSync = new(ModName) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        public static ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
        {
            ConfigEntry<T> configEntry = _self.Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }
        private ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true) => config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        void SetCfgValue<T>(Action<T> setter, ConfigEntry<T> config)
        {
            setter(config.Value);
            config.SettingChanged += (_, _) => setter(config.Value);
        }

        #region values
        static ConfigEntry<string> bossListConfig;
        static ConfigEntry<float> rangeConfig;
        static ConfigEntry<float> BGAlfaConfig;
        static ConfigEntry<float> creditsDelayConfig;
        static ConfigEntry<string> creditsTextConfig;
        static ConfigEntry<float> creditsDefaultSpeedConfig;
        static ConfigEntry<float> creditsIncreasedSpeedConfig;
        static ConfigEntry<Toggle> creditsAvtoSkipConfig;
        static ConfigEntry<float> creditsAvtoSkipDelayConfig;
        static ConfigEntry<Toggle> creditsPreventSkipConfig;
        static ConfigEntry<float> creditsMinSkipTimeConfig;
        static ConfigEntry<Toggle> creditsShowVideoConfig;
        static ConfigEntry<Toggle> creditsShowVideoAndTextConfig;
        static ConfigEntry<string> creditsVideoUrlConfig;

        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        static float range = 30f;
        public static List<string> bossList = new();
        public static float BGAlfa = 0.93f;
        public static float creditsDelay = 3.5f;
        public static float creditsDefaultSpeed = 0.1f;
        public static float creditsIncreasedSpeed = 0.6f;
        public static bool creditsAvtoSkip = false;
        public static float creditsAvtoSkipDelay = 5f;
        public static bool creditsPreventSkip = false;
        public static float creditsMinSkipTime = 3f;
        public static bool creditsShowVideo = false;
        public static bool creditsShowVideoAndText = false;
        public static string creditsVideoUrl;



        public static bool canSkip = false;
        #endregion

        public static string creditsTextPath = Path.Combine(Paths.ConfigPath, "com.Frogger.FinalCredits.CreditsText.json");
        #endregion

        private void Awake()
        {
            _self = this;
            JSON.Parameters = new JSONParameters
            {
                UseExtensions = false,
                SerializeNullValues = false,
                DateTimeMilliseconds = false,
                UseUTCDateTime = true,
                UseOptimizedDatasetSchema = true,
                UseValuesOfEnums = true
            };
            _embeddedResourceBundle = LoadAssetBundleFromResources("credits", Assembly.GetExecutingAssembly());

            defaultFond = _embeddedResourceBundle.LoadAsset<GameObject>("Credits").transform.Find("Credits").Find("ContactInfo_OLD").Find("Irongate").Find("Text").GetComponent<Text>().font;
            norseboldFond = _embeddedResourceBundle.LoadAsset<GameObject>("Credits").transform.Find("Credits").Find("ContactInfo_OLD").Find("Irongate").GetComponent<Text>().font;

            unityBlockPrefab = _embeddedResourceBundle.LoadAsset<GameObject>("block").AddComponent<CustomCreditsData.UnityBlock>();
            unityBlockPrefab.title = unityBlockPrefab.transform.Find("Title").Find("Text").GetComponent<Text>();
            unityBlockPrefab.contend = unityBlockPrefab.transform.Find("Contend").Find("Text").GetComponent<Text>();



            #region config
            Config.SaveOnConfigSet = false;

            bossListConfig = config("General", "BossList", "GoblinKing", "List of bosses\nExample: Eikthyr, Bonemass. \nWhen killing one of them, the credits appear");
            BGAlfaConfig = config("General", "Alfa Of BG", BGAlfa, "A value between 0 and 1.");
            creditsDelayConfig = config("General", "Credits Delay", creditsDelay, "The delay between the death of the boss and the showing of the credits");
            rangeConfig = config("General", "Radius", range, "Radius from the killed boss\nPlayers in this radius from the killed boss will be shown the credits.");
            creditsDefaultSpeedConfig = config("General", "Default Credits Speed", creditsDefaultSpeed, "The speed at which the credits will scroll");
            creditsIncreasedSpeedConfig = config("General", "Increased Credits Speed", creditsIncreasedSpeed, "The speed at which the credits will scroll when the mouse button is pressed");
            creditsAvtoSkipConfig = config("General", "Avto Skip", creditsAvtoSkip ? Toggle.On : Toggle.Off, "Determines whether the titles will be closed at the end.\nDoes not works with video.");
            creditsAvtoSkipDelayConfig = config("General", "Avto Skip Delay", creditsAvtoSkipDelay, "Delay in automatic closing of titles.\nDoes not works with video.");
            creditsPreventSkipConfig = config("General", "Prevent Skip", creditsPreventSkip ? Toggle.On : Toggle.Off, "Can the user skip the credits without waiting for them to finish or some time.");
            creditsMinSkipTimeConfig = config("General", "Min Skip Time", creditsMinSkipTime, "The time after which it will be possible to skip the remaining credits.");
            creditsShowVideoConfig = config("General", "Show Video", creditsShowVideo ? Toggle.On : Toggle.Off, "Show video instead of titles. \nSpecify the url to it in VideoUrl.");
            creditsShowVideoAndTextConfig = config("General", "Show Text with Video", creditsShowVideoAndText ? Toggle.On : Toggle.Off, "Show video and text. Video on the background and text on top.");
            creditsVideoUrlConfig = config("General", "Video Url", "", "Specify the url to the video being played instead of the titles.");
            creditsTextConfig = config("General", "DONT TOUCH", JSON.ToNiceJSON(new CustomCreditsData
            {
                blocks = new()
                {
                    new CustomCreditsData.Block()
                    {
                        title = new CustomCreditsData.Block.BlockText()
                        {
                            size = 60,
                            width = 400,
                            font = "Norsebold",
                            text = "IRON GATE"
                        },
                        contend = new CustomCreditsData.Block.BlockText()
                        {
                            size = 35,
                            width = 400,
                            font = "Default",
                            text = "Richard Svensson Henrik Törnqvist Robin Eyre Erik Ambring Lisa Tveit Kolfjord Jonathan Smårs Andreas Thomasson Christoffer Solgevik Jens Hellström Josefin Berntsson"
                        }
                    },
                    new CustomCreditsData.Block()
                    {
                        title = new CustomCreditsData.Block.BlockText()
                        {
                            size = 60,
                            width = 500,
                            font = "Norsebold",
                            text = "Coffee Stain Publishing"
                        },
                        contend = new CustomCreditsData.Block.BlockText()
                        {
                            size = 35,
                            width = 400,
                            font = "Default",
                            text = "<color=orange>Publishing Producer</color> Sebastian Badylak <color=orange> Publishing Team </color> Albert Säfström Angelica Uhlán Anton Westbergh Daniel Kaplan Emilia Oscarsson Joel Rydholm Johannes Aspeby Linus Sjöholm Nikhat Ali Tim Badylak"
                        }
                    },
                    new CustomCreditsData.Block()
                    {
                        title = new CustomCreditsData.Block.BlockText()
                        {
                            size = 60,
                            width = 550,
                            font = "Norsebold",
                            text = "Additional Contributors"
                        },
                        contend = new CustomCreditsData.Block.BlockText()
                        {
                            size = 35,
                            width = 400,
                            font = "Default",
                            text = "Magnus Noren Mike DaksGrace Yang"
                        }
                    },
                    new CustomCreditsData.Block()
                    {
                        title = new CustomCreditsData.Block.BlockText()
                        {
                            size = 45,
                            width = 550,
                            font = "Default",
                            text = "Story and Writing"
                        },
                        contend = new CustomCreditsData.Block.BlockText()
                        {
                            size = 35,
                            width = 350,
                            font = "Default",
                            text = "Lee Williams "
                        }
                    },
                    new CustomCreditsData.Block()
                    {
                        title = new CustomCreditsData.Block.BlockText()
                        {
                            size = 45,
                            width = 550,
                            font = "Default",
                            text = "Audio and Sound"
                        },
                        contend = new CustomCreditsData.Block.BlockText()
                        {
                            size = 35,
                            width = 350,
                            font = "Default",
                            text = "Dennis Filatov"
                        }
                    },
                    new CustomCreditsData.Block()
                    {
                        title = new CustomCreditsData.Block.BlockText()
                        {
                            size = 45,
                            width = 550,
                            font = "Default",
                            text = "Soundtrack"
                        },
                        contend = new CustomCreditsData.Block.BlockText()
                        {
                            size = 35,
                            width = 650,
                            font = "Default",
                            text = "Patrik Jarlestam(Composer) Phillippa Murphy - Haste(Clarinet and Viola) Jenean Lee(Cello) Michael H Dixon(French Horn) "
                        }
                    },
                    new CustomCreditsData.Block()
                    {
                        title = new CustomCreditsData.Block.BlockText()
                        {
                            size = 45,
                            width = 550,
                            font = "Default",
                            text = "Translators"
                        },
                        contend = new CustomCreditsData.Block.BlockText()
                        {
                            size = 35,
                            width = 350,
                            font = "Default",
                            text = "Jens Hellström Fredrik Lind Kohadon Oğuzhan RazielNisroc Kayan Bahtiyar ''Molly Prime'' Hamraev Andrii Raboshchuk Sergii Raboshchuk Taras Syniuk Björn Sigmundsson Brechler Zsolt Bjørn Viking34 Lund Grigoriy Nikolov Gabriel Pavić Bram Degryse Alessandro Rebuttini Dominik Gygram Dahmani Troidex Vojtěch ArcadeX Hlávka Marcin Tarnecki Mathias Hein Joakim Skjær Svendsen Lee Myeongho Veikka Ritola Mirella Meymi L. Tomáš Petráček, Tartarus Cast Nikita focus_xd Mileshniko Atskiato Fedor Nikitin Kevin J Park Gandalf Hassel Cezary Kamiński Jakub Holvr Lutowski Simonas Dimavičius Vladimír Rimvaldi Ščurka Grace Yang Arkam Simon Polenz Jaemin Kyle Ahn Canidiz Deniz Kantar Medina Blázquez Kim Kil yong Horacy Muszyński Valur Kári Óskarsson Eden Frost Valentin Sandberg Panciu Bonciu Felix Anghel Mircea Eugen Daniel Judas Saku Salonen Roger H.Nobrega"
                        }
                    }
                }
            }), "Please edit the generated json file in the configuration folder.");

            SetupWatcherOnConfigFile();

            CreateCredits();

            Config.SettingChanged += (_, _) => { UpdateConfiguration(); };
            Config.ConfigReloaded += (_, _) => { UpdateConfiguration(); };

            Config.SaveOnConfigSet = true;
            Config.Save();
            #endregion

            harmony.PatchAll();
        }
        private void Update()
        {
            float speed = 0.1f;

            if(Input.GetKeyDown(KeyCode.Escape) && creditsGameObject.activeInHierarchy)
            {
                if (canSkip) CloseCredits();
                else
                {
                    Menu.instance?.Show();
                    Game.Pause();
                    Hud.m_instance.m_userHidden = true;
                }
            }
            if ((Input.GetMouseButton(0) || Input.GetMouseButton(1)) && creditsGameObject.activeInHierarchy)
            {
                speed = creditsIncreasedSpeed;
            }
            else
            {
                speed = creditsDefaultSpeed;
            }
            #region Scrolling credits
            if (creditsParrentGameObject && creditsGameObject && creditsGameObject.activeInHierarchy)
            {
                RectTransform rectTransform = creditsParrentGameObject.transform as RectTransform; 
                Vector3[] array = new Vector3[4];
                creditsContactInfo.GetWorldCorners(array);
                Vector3[] array2 = new Vector3[4];
                rectTransform.GetWorldCorners(array2);
                float num2 = array2[1].y - array2[0].y;
                if (array[3].y < num2/* * 0.5*/)
                {
                    //Debug($"array[3].y = {array[3].y},\nnum2 = {num2}");
                    Vector3 position = creditsContactInfo.position;
                    position.y += Time.unscaledDeltaTime * speed * num2;
                    creditsContactInfo.position = position;
                    if (creditsBGImage?.color.a < BGAlfa) creditsBGImage.color = new(creditsBGImage.color.r, creditsBGImage.color.b, creditsBGImage.color.g, creditsBGImage.color.a + Time.unscaledDeltaTime * 0.5f);
                }
                else
                {
                    canSkip = true;
                    creditsCloseButton.transform.parent.gameObject.SetActive(true);
                    if (creditsAvtoSkip) CloseCreditsWithDelay();
                }

            }
            #endregion
        }


        #region Config
        public void SetupWatcherOnConfigFile()
        {
            FileSystemWatcher fileSystemWatcherOnConfig = new(Paths.ConfigPath, ConfigFileName);
            fileSystemWatcherOnConfig.Changed += ConfigChanged;
            fileSystemWatcherOnConfig.IncludeSubdirectories = true;
            fileSystemWatcherOnConfig.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            fileSystemWatcherOnConfig.EnableRaisingEvents = true;
        }
        public void SetupWatcherOnCreditsText()
        {
            if (!ZNet.instance.IsServer()) return;

            if (!File.Exists(creditsTextPath)) File.WriteAllText(creditsTextPath, JSON.ToNiceJSON(new CustomCreditsData
            {
                blocks = new()
                {
                    new CustomCreditsData.Block()
                    {
                        title = new CustomCreditsData.Block.BlockText()
                        {
                            size = 60,
                            width = 400,
                            font = "Norsebold",
                            text = "IRON GATE"
                        },
                        contend = new CustomCreditsData.Block.BlockText()
                        {
                            size = 35,
                            width = 400,
                            font = "Default",
                            text = "Richard Svensson Henrik Törnqvist Robin Eyre Erik Ambring Lisa Tveit Kolfjord Jonathan Smårs Andreas Thomasson Christoffer Solgevik Jens Hellström Josefin Berntsson"
                        }
                    },
                    new CustomCreditsData.Block()
                    {
                        title = new CustomCreditsData.Block.BlockText()
                        {
                            size = 60,
                            width = 500,
                            font = "Norsebold",
                            text = "Coffee Stain Publishing"
                        },
                        contend = new CustomCreditsData.Block.BlockText()
                        {
                            size = 35,
                            width = 400,
                            font = "Default",
                            text = "<color=orange>Publishing Producer</color> Sebastian Badylak <color=orange> Publishing Team </color> Albert Säfström Angelica Uhlán Anton Westbergh Daniel Kaplan Emilia Oscarsson Joel Rydholm Johannes Aspeby Linus Sjöholm Nikhat Ali Tim Badylak"
                        }
                    },
                    new CustomCreditsData.Block()
                    {
                        title = new CustomCreditsData.Block.BlockText()
                        {
                            size = 60,
                            width = 550,
                            font = "Norsebold",
                            text = "Additional Contributors"
                        },
                        contend = new CustomCreditsData.Block.BlockText()
                        {
                            size = 35,
                            width = 400,
                            font = "Default",
                            text = "Magnus Noren Mike DaksGrace Yang"
                        }
                    },
                    new CustomCreditsData.Block()
                    {
                        title = new CustomCreditsData.Block.BlockText()
                        {
                            size = 45,
                            width = 550,
                            font = "Default",
                            text = "Story and Writing"
                        },
                        contend = new CustomCreditsData.Block.BlockText()
                        {
                            size = 35,
                            width = 350,
                            font = "Default",
                            text = "Lee Williams "
                        }
                    },
                    new CustomCreditsData.Block()
                    {
                        title = new CustomCreditsData.Block.BlockText()
                        {
                            size = 45,
                            width = 550,
                            font = "Default",
                            text = "Audio and Sound"
                        },
                        contend = new CustomCreditsData.Block.BlockText()
                        {
                            size = 35,
                            width = 350,
                            font = "Default",
                            text = "Dennis Filatov"
                        }
                    },
                    new CustomCreditsData.Block()
                    {
                        title = new CustomCreditsData.Block.BlockText()
                        {
                            size = 45,
                            width = 550,
                            font = "Default",
                            text = "Soundtrack"
                        },
                        contend = new CustomCreditsData.Block.BlockText()
                        {
                            size = 35,
                            width = 650,
                            font = "Default",
                            text = "Patrik Jarlestam(Composer) Phillippa Murphy - Haste(Clarinet and Viola) Jenean Lee(Cello) Michael H Dixon(French Horn) "
                        }
                    },
                    new CustomCreditsData.Block()
                    {
                        title = new CustomCreditsData.Block.BlockText()
                        {
                            size = 45,
                            width = 550,
                            font = "Default",
                            text = "Translators"
                        },
                        contend = new CustomCreditsData.Block.BlockText()
                        {
                            size = 35,
                            width = 350,
                            font = "Default",
                            text = "Jens Hellström Fredrik Lind Kohadon Oğuzhan RazielNisroc Kayan Bahtiyar ''Molly Prime'' Hamraev Andrii Raboshchuk Sergii Raboshchuk Taras Syniuk Björn Sigmundsson Brechler Zsolt Bjørn Viking34 Lund Grigoriy Nikolov Gabriel Pavić Bram Degryse Alessandro Rebuttini Dominik Gygram Dahmani Troidex Vojtěch ArcadeX Hlávka Marcin Tarnecki Mathias Hein Joakim Skjær Svendsen Lee Myeongho Veikka Ritola Mirella Meymi L. Tomáš Petráček, Tartarus Cast Nikita focus_xd Mileshniko Atskiato Fedor Nikitin Kevin J Park Gandalf Hassel Cezary Kamiński Jakub Holvr Lutowski Simonas Dimavičius Vladimír Rimvaldi Ščurka Grace Yang Arkam Simon Polenz Jaemin Kyle Ahn Canidiz Deniz Kantar Medina Blázquez Kim Kil yong Horacy Muszyński Valur Kári Óskarsson Eden Frost Valentin Sandberg Panciu Bonciu Felix Anghel Mircea Eugen Daniel Judas Saku Salonen Roger H.Nobrega"
                        }
                    }
                }
            }));

            creditsTextConfig.Value = File.ReadAllText(creditsTextPath);

            FileSystemWatcher fileSystemWatcherOnCreditsText = new(Paths.ConfigPath, "com.Frogger.FinalCredits.CreditsText.json");
            fileSystemWatcherOnCreditsText.Changed += CreditsTextChanged;
            fileSystemWatcherOnCreditsText.IncludeSubdirectories = true;
            fileSystemWatcherOnCreditsText.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            fileSystemWatcherOnCreditsText.EnableRaisingEvents = true;
        }
        private void ConfigChanged(object sender, FileSystemEventArgs e)
        {
            if ((DateTime.Now - this.LastConfigChange).TotalSeconds <= 5.0)
            {
                return;
            }
            LastConfigChange = DateTime.Now;
            try
            {
                Config.Reload();
                Debug("Reloading Config...");
            }
            catch
            {
                DebugError("Can't reload Config");
            }
        }
        private void CreditsTextChanged(object sender, FileSystemEventArgs e)
        {
            if ((DateTime.Now - this.LastConfigChange).TotalSeconds <= 5.0)
            {
                return;
            }
            LastConfigChange = DateTime.Now;
            try
            {
                creditsTextConfig.Value = File.ReadAllText(creditsTextPath);
                ConfigChanged(null, null);
            }
            catch
            {
                DebugError("Can't reload Config");
            }
        }
        private void UpdateConfiguration()
        {
            Task task = null;
            task = Task.Run(() =>
            {
                bossList = new();
                string bossListString = bossListConfig.Value.Replace(" ", "");
                bossListString = bossListString.Replace("[", "");
                bossListString = bossListString.Replace("]", "");
                bossListString = bossListString.Replace("/", "");
                string[] bosses = bossListString.Split(',');
                foreach (string boss in bosses) bossList.Add(boss);

                BGAlfa = BGAlfaConfig.Value;
                range = rangeConfig.Value;
                creditsDelay = creditsDelayConfig.Value;
                creditsDefaultSpeed = creditsDefaultSpeedConfig.Value;
                creditsIncreasedSpeed = creditsIncreasedSpeedConfig.Value;
                creditsAvtoSkip = creditsAvtoSkipConfig.Value == Toggle.On;
                creditsAvtoSkipDelay = creditsAvtoSkipDelayConfig.Value;
                creditsPreventSkip = creditsPreventSkipConfig.Value == Toggle.On;
                creditsMinSkipTime = creditsMinSkipTimeConfig.Value;
                creditsShowVideo = creditsShowVideoConfig.Value == Toggle.On;
                creditsShowVideoAndText = creditsShowVideoAndTextConfig.Value == Toggle.On;
                creditsVideoUrl = creditsVideoUrlConfig.Value;

                creditsVideoPlayer.url = creditsVideoUrl;
                UpdateCreditsTextCorut();

            });

            Task.WaitAll();

            UpdateCreditsText();

            Debug("Configuration Received");
        }
        #endregion
        #region Path
        [HarmonyPatch]
        public static class Pacth
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
            private static void ZNetScenePatch(ObjectDB __instance)
            {
                if (SceneManager.GetActiveScene().name == "main")
                {
                    _self.Config.Reload();

                    if (!ZNet.instance.IsServer()) return;

                    _self.SetupWatcherOnCreditsText();
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(Player), nameof(Player.Awake))]
            private static void PlayerControllerPatch(Player __instance)
            {
                if (!__instance.IsPlayer()) return;
                __instance.m_nview.Register("RPC_ShowCredits", new Action<long>(_self.RPC_ShowCredits));
            }

            [HarmonyPostfix]            
            [HarmonyPatch(typeof(Character), nameof(Character.OnDeath))]
            private static void CharacterOnDeathPatch(Character __instance)
            {
                List<Player> playersInRage = new();
                Player.GetPlayersInRange(__instance.transform.position, range, playersInRage);

                if (!__instance.IsPlayer()// && __instance.IsBoss()
                    )
                {
                    foreach (var bossName in bossList)
                    {
                        if(bossName.Contains(__instance.m_name) || bossName.Contains(__instance.name.Replace("(Clone)", "")))
                        {
                            playersInRage.ForEach((Player pl) =>
                            {
                                try
                                {
                                    pl.m_nview.InvokeRPC("RPC_ShowCredits");
                                }
                                catch (Exception)
                                {
                                    pl.m_nview.Register("RPC_ShowCredits", new Action<long>(_self.RPC_ShowCredits));
                                    pl.m_nview.InvokeRPC("RPC_ShowCredits");
                                    throw;
                                }

                            });

                            _self.Debug("Right Boss Dead");

                            return;
                        }
                    }

                    _self.Debug($"Wrong Boss Dead\nName = {__instance.m_name}/{__instance.name.Replace("(Clone)", "")}");
                }
            }
        }
        #endregion
        #region Credits
        private void CreateCredits()
        {
            GameObject credits = Instantiate(_embeddedResourceBundle.LoadAsset<GameObject>("Credits"));
            DontDestroyOnLoad(credits);
            creditsGameObject = credits.transform.Find("Credits").gameObject;

            creditsBGImage = creditsGameObject.GetComponent<Image>();
            creditsBGImage.color = new(0, 0, 0, 0.3f);

            creditsContactInfo = creditsGameObject.transform.Find("ContactInfo") as RectTransform;
            creditsParrentGameObject = credits;

            creditsCloseButton = creditsGameObject.transform.Find("Back-panel").Find("BackButtonm").gameObject.GetComponent<Button>();
            creditsVideoImage = creditsGameObject.transform.Find("VideoImage").gameObject.GetComponent<RawImage>();
            creditsVideoPlayer = creditsGameObject.transform.Find("VideoImage").gameObject.GetComponent<VideoPlayer>();

            Text creditsCloseButtonText = creditsCloseButton.transform.Find("Text").GetComponent<Text>();
            creditsCloseButtonText.text = Localization.instance.Localize("$menu_back");

            creditsCloseButton.onClick = new();
            creditsCloseButton.onClick.AddListener(CloseCredits);

            UpdateCreditsText();

            creditsGameObject.SetActive(false);
        }
        public void RPC_ShowCredits(long senger = new()) => StartCoroutine(ShowCreditsIEnumerator());
        public void CloseCreditsWithDelay() => StartCoroutine(CloseCreditsIEnumerator());
        public void CloseCredits()
        {
            creditsGameObject.SetActive(false);
            Menu.instance?.Hide();
            Game.Unpause();
            if (Hud.m_instance != null) Hud.m_instance.m_userHidden = false;
        }
        private IEnumerator ShowCreditsIEnumerator()
        {
            yield return new WaitForSecondsRealtime(creditsDelay);

            canSkip = false;

            GameObject credits = creditsGameObject;
            creditsContactInfo.position = new(960, 0, 0);
            creditsBGImage.color = new(0, 0, 0, 0f);
            credits.SetActive(true);
            Menu.instance?.Show();
            Game.Pause();
            Hud.m_instance.m_userHidden = true;

            if (creditsShowVideo)
            {
                creditsVideoImage.gameObject.SetActive(true);
                if(!creditsShowVideoAndText) creditsContactInfo.gameObject.SetActive(false);
                if(creditsShowVideoAndText) creditsContactInfo.gameObject.SetActive(true);
            }
            else
            {
                creditsContactInfo.gameObject.SetActive(true);
                creditsVideoImage.gameObject.SetActive(false);
            }

            if (!creditsPreventSkip)
            {
                creditsCloseButton.transform.parent.gameObject.SetActive(true);
                canSkip = true;
            }
            else if(creditsPreventSkip)
            {
                creditsCloseButton.transform.parent.gameObject.SetActive(false);
                yield return new WaitForSecondsRealtime(creditsMinSkipTime);
                canSkip = true;
                creditsCloseButton.transform.parent.gameObject.SetActive(true);
            }
        }
        private IEnumerator CloseCreditsIEnumerator()
        {
            if(creditsAvtoSkipDelay >= 1f) yield return new WaitForSecondsRealtime(creditsAvtoSkipDelay);

            CloseCredits();
        }
        private void UpdateCreditsText()
        {
            float timeScale = Time.timeScale;
            bool isCreditsActive = creditsGameObject.activeSelf;

            creditsGameObject.SetActive(true);

            foreach (CustomCreditsData.UnityBlock unityBlock in creditsContactInfo.GetComponentsInChildren<CustomCreditsData.UnityBlock>())
            {
                Destroy(unityBlock.title.GetComponent<ContentSizeFitter>());
                Destroy(unityBlock.contend.GetComponent<ContentSizeFitter>());
                Destroy(unityBlock.title.transform.parent.GetComponent<ContentSizeFitter>());
                Destroy(unityBlock.contend.transform.parent.GetComponent<ContentSizeFitter>());
                Destroy(unityBlock.title);
                Destroy(unityBlock);
                Destroy(unityBlock.gameObject);
            }

            Time.timeScale = 1;

            for (int i = 0; i < customCredits.creditsData.blocks.Count; i++)
            {
                CustomCreditsData.Block currentBlock = customCredits.creditsData.blocks[i];
                CustomCreditsData.UnityBlock unityBlock = Instantiate(unityBlockPrefab, creditsContactInfo.transform);

                unityBlock.title.text = currentBlock.title.text;
                unityBlock.title.fontSize = currentBlock.title.size;
                unityBlock.title.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, currentBlock.title.width);
                unityBlock.title.font = currentBlock.title.font == "Default" ? defaultFond : norseboldFond;

                unityBlock.contend.text = currentBlock?.contend?.text ?? "";
                unityBlock.contend.fontSize = currentBlock?.contend?.size ?? 25;
                unityBlock.contend.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, currentBlock.contend.width);
                unityBlock.contend.font = currentBlock?.contend?.font == "Default" ? defaultFond : norseboldFond;

                Debug($"UpdateCreditsText for {customCredits.creditsData.blocks[i].title.text}");
            }

            foreach (var contentSizeFitter in GetComponentsInParent<ContentSizeFitter>())
            {
                LayoutRebuilder.MarkLayoutForRebuild(contentSizeFitter.transform as RectTransform);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(creditsContactInfo.transform as RectTransform);            
            LayoutRebuilder.ForceRebuildLayoutImmediate(creditsContactInfo.transform as RectTransform);            
            LayoutRebuilder.ForceRebuildLayoutImmediate(creditsContactInfo.transform as RectTransform);            
            LayoutRebuilder.ForceRebuildLayoutImmediate(creditsContactInfo.transform as RectTransform);            
            LayoutRebuilder.ForceRebuildLayoutImmediate(creditsGameObject.transform as RectTransform);            
            LayoutRebuilder.ForceRebuildLayoutImmediate(creditsGameObject.transform as RectTransform);            

            Time.timeScale = timeScale;
            creditsGameObject.SetActive(isCreditsActive);
        }
        #endregion
        #region tools
        public void Debug(string msg)
        {
            Logger.LogInfo(msg);
        }
        public void DebugError(string msg)
        {
            Logger.LogError($"{msg} Write to the developer and moderator if this happens often.");
        }
        public AssetBundle LoadAssetBundleFromResources(string bundleName, Assembly resourceAssembly)
        {
            if (resourceAssembly == null)
            {
                throw new ArgumentNullException("Parameter resourceAssembly can not be null.");
            }
            string text = null;
            try
            {
                text = resourceAssembly.GetManifestResourceNames().Single((string str) => str.EndsWith(bundleName));
            }
            catch (Exception)
            {
            }
            if (text == null)
            {
                Logger.LogError("AssetBundle " + bundleName + " not found in assembly manifest");
                return null;
            }
            AssetBundle result;
            using (Stream manifestResourceStream = resourceAssembly.GetManifestResourceStream(text))
            {
                result = AssetBundle.LoadFromStream(manifestResourceStream);
            }
            return result;
        }
        public class ConfigurationManagerAttributes
        {
            public int? Order;
            public bool? HideSettingName;
            public bool? HideDefaultButton;
            public string? DispName;
            public Action<ConfigEntryBase>? CustomDrawer;
        }
        public static Font GetFondByName(string name)
        {
            Font font = name switch
            {
                "Default" => defaultFond,
                "Norsebold" => norseboldFond,
                _ => defaultFond
            };
            return font;
        }
        public void UpdateCreditsTextCorut() => StartCoroutine(UpdateCreditsTextIEnumerator());
        private IEnumerator UpdateCreditsTextIEnumerator()
        {
            CustomCreditsData data = JSON.ToObject<CustomCreditsData>(creditsTextConfig.Value);

            yield return data;

            customCredits.creditsData = data;

            UpdateCreditsText();
        }
        #endregion
    }
}