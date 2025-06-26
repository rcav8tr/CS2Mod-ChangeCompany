using Colossal.Logging;
using Game;
using Game.Modding;
using System;
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

        /// <summary>
        /// One-time mod loading.
        /// </summary>
        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info($"{nameof(Mod)}.{nameof(OnLoad)} Version {ModAssemblyInfo.Version}");
            
            try
            {
                // Initialize translations.
                Translation.Initialize();

                // Create this mod's ChangeCompanySection which contains the logic
                // for a new section in the game's selected info view in the UI.
                // This is not a system that updates directly in a system update phase.
                // Instead, updates are handled by SelectedInfoUISystem.
                // The section just needs to be created.
                World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<ChangeCompanySection>();

                // Create and activate this mod's ChangeCompanySystem which contains the logic to change the company on a property.
                // In the game, this logic is normally executed in the GameSimulation phase.
                // Of course, the GameSimulation phase runs only when the simulation is running.
                // It is highly desired to allow the player to change companies while the game is paused.
                // Therefore, ChangeCompanySystem updates in the PostSimulation phase, which runs even when the game is paused.
                updateSystem.UpdateAt<ChangeCompanySystem>(SystemUpdatePhase.PostSimulation);

#if DEBUG
                // Get localized text from the game where the value is or contains specific text.
                //Colossal.Localization.LocalizationManager localizationManager = Game.SceneFlow.GameManager.instance.localizationManager;
                //foreach (System.Collections.Generic.KeyValuePair<string, string> keyValue in localizationManager.activeDictionary.entries)
                //{
                //    // Exclude assets.
                //    if (!keyValue.Key.StartsWith("Assets."))
                //    {
                //        //if (keyValue.Key.ToLower().Contains("concat"))
                //        //if (keyValue.Key.StartsWith("Resource"))
                //        if (keyValue.Value == "+")
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
                //    foreach (System.Collections.Generic.KeyValuePair<string, string> keyValue in localizationManager.activeDictionary.entries)
                //    {
                //        if (keyValue.Key == "SelectedInfoPanel.COMPANY_STORES")
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

            // Nothing to do here.  But implementation is required.
        }
    }
}
