using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Colossal.UI;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Game.Simulation;
using Game.UI.InGame;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Entities;

namespace ChangeCompany
{
    /// <summary>
    /// The main entry point for this mod.
    /// </summary>
    public class Mod : IMod
    {
        // Create a new log just for this mod.
        // This mod will have its own log file in the game's Logs folder.
        public static readonly ILog log = LogManager.GetLogger(ModAssemblyInfo.Name)
            .SetShowsErrorsInUI(true)                       // Show message in UI for severity level Error and above.
            .SetShowsStackTraceAboveLevels(Level.Error);    // Include stack trace for severity level Error and above.

        // The global settings for this mod.
        public static ModSettings ModSettings { get; set; }

        /// <summary>
        /// One-time mod loading.
        /// </summary>
        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info($"{nameof(Mod)}.{nameof(OnLoad)} Version {ModAssemblyInfo.Version}");
            
            try
            {
                // Register and load mod settings.
                ModSettings = new ModSettings(this);
                ModSettings.RegisterInOptionsUI();
                ModSettings.RegisterKeyBindings();
                AssetDatabase.global.LoadSettings(ModAssemblyInfo.Name, ModSettings, new ModSettings(this));
                ModSettings.Loaded();

                // Initialize translations.
                Translation.Initialize();

                // Add mod UI images directory to UI resource handler.
                // When the URI is used to access an image, the game forces the URI portion to lower case.
                // So make the URI lower case here to be compatible.
                if (!GameManager.instance.modManager.TryGetExecutableAsset(this, out ExecutableAsset modExecutableAsset))
                {
                    log.Error("Unable to get mod executable asset.");
                    return;
                }
                string assemblyPath = Path.GetDirectoryName(modExecutableAsset.path);
                string imagesPath = Path.Combine(assemblyPath, "Images");
                UIManager.defaultUISystem.AddHostLocation(ModAssemblyInfo.Name.ToLower(), imagesPath);

                // Create this mod's sections for the game's building info display in the UI.
                // These are not systems that update directly in a system update phase.
                // Instead, updates are handled by SelectedInfoUISystem which runs in UIUpdate phase.
                // The sections just need to be created.
                World defaultWorld = World.DefaultGameObjectInjectionWorld;
                ChangeCompanySection changeCompanySection = defaultWorld.GetOrCreateSystemManaged<ChangeCompanySection>();
                LockCompanySection   lockCompanySection   = defaultWorld.GetOrCreateSystemManaged<LockCompanySection  >();
                    
                // Use reflection to get the list of middle sections from SelectedInfoUISystem.
                FieldInfo fieldInfoMiddleSections = typeof(SelectedInfoUISystem).GetField("m_MiddleSections", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fieldInfoMiddleSections == null)
                {
                    log.Error($"{nameof(Mod)}.{nameof(OnLoad)} Unable to find middle sections in SelectedInfoUISystem.");
                    return;
                }
                SelectedInfoUISystem selectedInfoUISystem = defaultWorld.GetOrCreateSystemManaged<SelectedInfoUISystem>();
                List<ISectionSource> middleSections = (List<ISectionSource>)fieldInfoMiddleSections.GetValue(selectedInfoUISystem);
                if (middleSections == null)
                {
                    log.Error($"{nameof(Mod)}.{nameof(OnLoad)} Unable to get middle sections from SelectedInfoUISystem.");
                    return;
                }

                // Get the index of the game's CompanySection.
                int companySectionIndex = -1;
                for (int i = 0; i < middleSections.Count; i++)
                {
                    if (middleSections[i] is CompanySection)
                    {
                        companySectionIndex = i;
                        break;
                    }
                }

                // Check if game's CompanySection was found.
                if (companySectionIndex == -1)
                {
                    // Log an error and add this mod's sections to the end.
                    log.Error($"[{ModAssemblyInfo.Title}] Unable to find CompanySection in middle sections from SelectedInfoUISystem.");
                    middleSections.Add(changeCompanySection);
                    middleSections.Add(lockCompanySection);
                }
                else
                {
                    // Insert ChangeCompanySection and LockCompanySection right after the game's CompanySection.
                    // This is the order they will be displayed by the game.
                    middleSections.Insert(companySectionIndex + 1, changeCompanySection);
                    middleSections.Insert(companySectionIndex + 2, lockCompanySection);
                }

                // Activate this mod's ChangeCompanySystem which contains the logic to change or remove the company on a property.
                // In the game, this logic is normally executed in the GameSimulation phase.
                // Of course, the GameSimulation phase runs only when the simulation is running.
                // It is highly desired to allow the player to change companies while the game is paused.
                // Therefore, ChangeCompanySystem updates in the PostSimulation phase, which runs even when the game is paused.
                updateSystem.UpdateAt<ChangeCompanySystem>(SystemUpdatePhase.PostSimulation);

                // Activate this mod's production balance systems.
                // Run after the TimeSystem which updates the game date/time.
                updateSystem.UpdateAfter<ProductionBalanceSystem, TimeSystem>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateAt<ProductionBalanceUISystem>(SystemUpdatePhase.UIUpdate);


#if DEBUG
                // Get localized text from the game where the value is or contains specific text.
                //Colossal.Localization.LocalizationManager localizationManager = Game.SceneFlow.GameManager.instance.localizationManager;
                //foreach (KeyValuePair<string, string> keyValue in localizationManager.activeDictionary.entries)
                //{
                //    // Exclude assets.
                //    if (!keyValue.Key.StartsWith("Assets."))
                //    {
                //        //if (keyValue.Key.ToLower().Contains("year"))
                //        if (keyValue.Value == "B")
                //        {
                //            log.Info(keyValue.Key + "\t" + keyValue.Value);
                //        }
                //    }
                //}

                // For a specific localization key, get the localized text for each base game locale ID.
                //string[] localeIDs = new string[] { "en-US", "de-DE", "es-ES", "fr-FR", "it-IT", "ja-JP", "ko-KR", "pl-PL", "pt-BR", "ru-RU", "zh-HANS", "zh-HANT" };
                //foreach (string localeID in localeIDs)
                //{
                //    localizationManager.SetActiveLocale(localeID);
                //    foreach (KeyValuePair<string, string> keyValue in localizationManager.activeDictionary.entries)
                //    {
                //        if (keyValue.Key == "Options.INPUT_CONTROL[Keyboard.ctrl]")
                //        {
                //            log.Info(keyValue.Key + "\t" + localeID + "\t" + keyValue.Value);
                //            break;
                //        }
                //    }
                //}
                //localizationManager.SetActiveLocale("en-US");
#endif
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }

            log.Info($"{nameof(Mod)}.{nameof(OnLoad)} complete.");
        }

        /// <summary>
        /// One-time mod disposing.
        /// </summary>
        public void OnDispose()
        {
            log.Info($"{nameof(Mod)}.{nameof(OnDispose)}");

            // Unregister mod settings.
            ModSettings?.UnregisterInOptionsUI();
            ModSettings = null;
        }
    }
}
