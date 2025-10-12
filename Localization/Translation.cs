using Colossal;
using Colossal.Localization;
using Game.SceneFlow;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ChangeCompany
{
    /// <summary>
    /// Get translated text.
    /// </summary>
    public static class Translation
    {
        // Translations are in the Translation.csv file.
        // The translation file is an embedded resource in the mod DLL so that a separate file does not need to be downloaded with the mod.
        // The translation file was created and maintained using LibreOffice Calc.
        private const string TranslationFile = ModAssemblyInfo.Name + ".Localization.Translation.csv";

        // Translation file format:
        //      Line 1: blank,locale ID 1,locale ID 2,...,locale ID n
        //      Line 2: translation key 1,translated text 1,translated text 2,...,translated text n
        //      Line 3: translation key 2,translated text 1,translated text 2,...,translated text n
        //      ...
        //      Line m: translation key m-1,translated text 1,translated text 2,...,translated text n

        // Translation file notes:
        //      The first line in the file must contain locale IDs and therefore cannot be blank or a comment.
        //      The file must contain translations for the default locale ID.
        //      The file should contain translations for every locale ID supported by the base game.
        //      The file may contain translations for additional locale IDs.
        //      Locale IDs in the file may be in any order, except that the default locale ID must be first.
        //      A locale ID may not be duplicated.
        //      A blank line is skipped.
        //      A line with a blank translation key is skipped (except the first line).
        //      A line with a translation key that starts with the character (#) is considered a comment and is skipped.
        //      The file should contain a line for every translation key constant name (not value) in UITranslationKey.
        //      Translations keys are case sensitive.
        //      If a translation key is duplicated, then a newline and the translated text are appended to the previous translated text for that key.
        //      Any \n in the translated text is replaced with a newline.
        //      The file must not contain blank columns.
        //      Each locale ID, translation key, and translated text may or may not be enclosed in double quotes ("text").
        //      Spaces around the comma separators will be included in the translated text.
        //      To include a comma in the translated text, the translated text must be enclosed in double quotes ("te, xt").
        //      To include a double quote in the translated text, use two consecutive double quotes inside the double quoted translated text ("te""xt").
        //      Translated text that contains "@@" will use the translated text from the translation key after the "@@".
        //      The translation key referenced with "@@" must have been read prior to the line with the "@@".
        //      A translation key that starts with "@@" is a temporary translation key that can be referenced in other translated text with "@@".
        //      Translated text cannot be blank for the default locale.
        //      Blank translated text in a non-default locale will use the translated text for the default locale.
        //      Translated text that starts with "$$" will retrieve the game's translated text for whatever key follows the "$$".

        // Default locale ID.
        private const string DefaultLocaleID = "en-US";

        // Translations for a single locale.
        // Dictionary key is the translation key.
        // Dictionary value is the translated text for that translation key.
        private class Translations : Dictionary<string, string>
        {
            public Translations(string[] translationKeys = null)
            {
                // If translation keys are specified, initialize translation values with blanks for all translation keys.
                if (translationKeys != null)
                {
                    foreach (string translationKey in translationKeys)
                    {
                        Add(translationKey, "");
                    }
                }
            }
        }

        // Maintain a list of locale IDs that this mod added to the localization manager.
        private static readonly List<string> _addedLocaleIDs = new();

        // The game's localization manager.
        private static readonly LocalizationManager _localizationManager = GameManager.instance.localizationManager;
        
        /// <summary>
        /// A localization source that can be added to the game.
        /// </summary>
        private class LocalizationSource : IDictionarySource
        {
            // The translations for this localization source.
            private readonly Translations _translations = new();

            // Prevent instantiation without translations.
            private LocalizationSource() { }

            // Instantiate with translations.
            public LocalizationSource(Translations translations)
            {
                // Do each translation key.
                foreach (string key in translations.Keys)
                {
                    // Localization source keys are the constant values (not names) from UITranslationKey.
                    _translations.Add((string)typeof(UITranslationKey).GetField(key).GetValue(null), translations[key]);
                }
            }

            /// <summary>
            /// Return the translations for the localization source.
            /// </summary>
            public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
            {
                return _translations;
            }

            public void Unload()
            {
                // Nothing to do here, but implementation is required.
            }
        }

        /// <summary>
        /// Initialize the translations from the translation file.
        /// </summary>
        public static void Initialize()
        {
            try
            {
                Mod.log.Info($"{nameof(Translation)}.{nameof(Initialize)}");

                // Make sure the translation CSV file exists.
                if (!Assembly.GetExecutingAssembly().GetManifestResourceNames().Contains(TranslationFile))
                {
                    Mod.log.Error($"Translation file [{TranslationFile}] does not exist in the assembly.");
                    return;
                }

                // Set initial translations for the active locale.
                SetTranslationsForActiveLocale();

                // When active dictionary changes, set translations for the newly active locale.
                // A "dictionary change" includes a change to the content of the active dictionary or a change to the active locale ID.
                // This mod cares only about changes to the active locale ID and does not handle a change to the content.
                _localizationManager.onActiveDictionaryChanged += SetTranslationsForActiveLocale;
            }
            catch(Exception ex)
            {
                Mod.log.Error(ex);
            }
        }

        /// <summary>
        /// Set translations for the active locale.
        /// </summary>
        private static void SetTranslationsForActiveLocale()
        {
            try
            {
                // If translations for the active locale were already added, do not load translations again.
                string activeLocaleID = _localizationManager.activeLocaleId;
                if (_addedLocaleIDs.Contains(activeLocaleID))
                {
                    return;
                }
                
                Mod.log.Info($"Setting mod translations for active locale: {activeLocaleID}");

                // Read all the text from the translation CSV file.
                string fileText;
                using (Stream fileStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(TranslationFile))
                {
                    using (StreamReader fileReader = new(fileStream, Encoding.UTF8))
                    {
                        fileText = fileReader.ReadToEnd();
                    }
                }

                // Split the file text into lines.
                string[] lines = fileText.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.None);

                // Get locale IDs from the first line of the file.
                if (!GetFileLocaleIDs(lines[0], out List<string> fileLocaleIDs))
                {
                    return;
                }

                // Get translation keys.
                // These are the names (not values) of all the public fields in UITranslationKey.
                FieldInfo[] fields = typeof(UITranslationKey).GetFields(BindingFlags.Public | BindingFlags.Static);
                string[] translationKeys = new string[fields.Length];
                for (int i = 0; i < fields.Length; i++)
                {
                    translationKeys[i] = fields[i].Name;
                }

                // Create new regular and temporary translations for the default and active locales.
                // The translations for the default locale are obtained regardless of the active locale
                // so that if a translation is blank for the active locale, the translation for the default locale can be used.
                Translations translationsRegularDefaultLocale   = new(translationKeys);
                Translations translationsRegularActiveLocale    = new(translationKeys);
                Translations translationsTemporaryDefaultLocale = new();
                Translations translationsTemporaryActiveLocale  = new();

                // Initialize count of translation keys.
                Dictionary<string, int> translationKeyCount = new();
                foreach (string translationKey in translationKeys)
                {
                    translationKeyCount.Add(translationKey, 0);
                }

                // Process each subsequent line in the file.
                for (int i = 1; i < lines.Length; i++)
                {
                    ProcessTranslationLine(
                        lines[i],
                        activeLocaleID,
                        fileLocaleIDs,
                        translationsRegularDefaultLocale,
                        translationsRegularActiveLocale,
                        translationsTemporaryDefaultLocale,
                        translationsTemporaryActiveLocale,
                        translationKeys,
                        translationKeyCount);
                }

                // Each translation key must be defined.
                foreach (string translationkey in translationKeys)
                {
                    if (translationKeyCount[translationkey] == 0)
                    {
                        Mod.log.Warn($"Translation key [{translationkey}] is not defined in the translation file.");
                        translationsRegularActiveLocale[translationkey] = translationkey;
                    }
                }

                // Remember that the active locale ID was (i.e. is about to be) added.
                // It is important to do this BEFORE calling AddSource below.
                // AddSource causes onActiveDictionaryChanged to be triggered, which causes this method to be called again.
                // Remembering the locale ID first prevents this method from trying to add the source again.
                _addedLocaleIDs.Add(activeLocaleID);

                // Add localization source to the game for active locale using regular translations for the active or default locale.
                if (fileLocaleIDs.Contains(activeLocaleID))
                {
                    _localizationManager.AddSource(activeLocaleID, new LocalizationSource(translationsRegularActiveLocale));
                }
                else
                {
                    Mod.log.Warn($"Translation file does not define locale ID [{activeLocaleID}]. Using default translations.");
                    _localizationManager.AddSource(activeLocaleID, new LocalizationSource(translationsRegularDefaultLocale));
                }
            }
            catch(Exception ex)
            {
                Mod.log.Error(ex);
            }
        }

        /// <summary>
        /// Get locale IDs from the first line in the file.
        /// </summary>
        private static bool GetFileLocaleIDs(string firstLine, out List<string> fileLocaleIDs)
        {
            // No file locale IDs to start.
            fileLocaleIDs = new();

            // First line cannot be blank or a comment.
            if (firstLine.Trim().Length == 0 || firstLine.StartsWith("#"))
            {
                Mod.log.Error("Translation file first line is blank or a comment. Expecting locale IDs on the first line.");
                return false;
            }

            // Initialize count of locale IDs.
            Dictionary<string, int> localeIDCount = new();

            // Create a reader on the first line.
            using (StringReader reader = new(firstLine))
            {
                // Read the first value, which must be blank.
                string firstValue = ReadCSVValue(reader);
                if (firstValue.Length != 0)
                {
                    Mod.log.Error("Translation file first line first value must be blank.");
                    return false;
                }

                // Read locale IDs as long as there are non-blank values on the line.
                bool firstLocaleID = true;
                string localeID = ReadCSVValue(reader).Trim();
                while (localeID.Length != 0)
                {
                    // Check if locale ID already exists.
                    if (localeIDCount.ContainsKey(localeID))
                    {
                        // Count locale ID occurrences.
                        localeIDCount[localeID]++;
                    }
                    else
                    {
                        // Add the new locale ID with an initial count of 1.
                        localeIDCount.Add(localeID, 1);
                    }

                    // Check that the first locale ID in the line is the default locale ID.
                    if (firstLocaleID && localeID != DefaultLocaleID)
                    {
                        Mod.log.Error($"Translation file must have default locale ID [{DefaultLocaleID}] defined first.");
                        return false;
                    }

                    // Get next locale ID from the line.
                    localeID = ReadCSVValue(reader);
                    firstLocaleID = false;
                }
            }

            // Each locale ID must be defined exactly once.
            foreach (string localeID in localeIDCount.Keys)
            {
                if (localeIDCount[localeID] != 1)
                {
                    Mod.log.Error($"Translation file defines locale ID [{localeID}] {localeIDCount[localeID]} times.  Expecting 1 time.");
                    return false;
                }
            }

            // File locale IDs are valid.
            fileLocaleIDs = localeIDCount.Keys.ToList();
            return true;
        }

        /// <summary>
        /// Process a line from the translation file.
        /// </summary>
        private static void ProcessTranslationLine
        (
            string line,
            string activeLocaleID,
            List<string> fileLocaleIDs,
            Translations translationsRegularDefaultLocale,
            Translations translationsRegularActiveLocale,
            Translations translationsTemporaryDefaultLocale,
            Translations translationsTemporaryActiveLocale,
            string[] translationKeys,
            Dictionary<string, int> translationKeyCount
        )
        {
            // Skip blank lines.
            if (line.Trim().Length == 0)
            {
                return;
            }

            // Create a string reader on the line.
            using (StringReader reader = new(line))
            {
                // The first value in the line is the translation key.
                string translationKey = ReadCSVValue(reader);

                // Skip lines with a blank or comment translation key.
                if (translationKey.Trim().Length == 0 || translationKey.StartsWith("#"))
                {
                    return;
                }

                // Check for temporary key.
                bool isTemporaryKey = translationKey.StartsWith("@@");
                if (isTemporaryKey)
                {
                    // Check if temporary key does not already exist.
                    if (!translationsTemporaryDefaultLocale.ContainsKey(translationKey))
                    {
                        // Add temporary translation key to default and active locales.
                        // The temporary translation key is saved with its @@ prefix.
                        translationsTemporaryDefaultLocale.Add(translationKey, "");
                        translationsTemporaryActiveLocale .Add(translationKey, "");
                    }
                }
                else
                {
                    // Check translation key.
                    if (translationKeys.Contains(translationKey))
                    {
                        // Count translation key occurrences.
                        translationKeyCount[translationKey]++;
                    }
                    else
                    {
                        // Skip this invalid translation key.
                        Mod.log.Warn($"Translation file contains translation key [{translationKey}] which is not defined in the mod.");
                        return;
                    }
                }

                // Do each locale in the file.
                foreach (string fileLocaleID in fileLocaleIDs)
                {
                    // Get the translated text.
                    // Need to get the text even if the locale is skipped so the reader is advanced to the next locale.
                    string translatedText = ReadCSVValue(reader);

                    // Skip file locale that is not the default or the active locale.
                    if (!(fileLocaleID == DefaultLocaleID || fileLocaleID == activeLocaleID))
                    {
                        continue;
                    }

                    // Check for blank translated text.
                    if (string.IsNullOrEmpty(translatedText))
                    {
                        // Check for default locale.
                        if (fileLocaleID == DefaultLocaleID)
                        {
                            // For default locale, warn and use the key as the translated text.
                            Mod.log.Warn($"Translation for key [{translationKey}] must be defined for default locale [{DefaultLocaleID}].");
                            translatedText = translationKey;
                        }
                        else
                        {
                            // Other than default locale.
                            // Use temporary or regular translated text from default locale without warning.
                            // This is a feature, not an error.
                            translatedText = isTemporaryKey ? 
                                translationsTemporaryDefaultLocale[translationKey] :
                                translationsRegularDefaultLocale  [translationKey];
                        }
                    }

                    // Check for $$ prefix in the translated text.
                    if (translatedText.StartsWith("$$"))
                    {
                        // Get game translation key after the $$ prefix.
                        string gameTranslationKey = translatedText.Substring(2);

                        // Get the game's translation for the key.
                        if (_localizationManager.activeDictionary.TryGetValue(gameTranslationKey, out string gameTranslatedText))
                        {
                            // Use the game translated text.
                            translatedText = gameTranslatedText;
                        }
                        else
                        {
                            Mod.log.Warn($"Game translation key [{gameTranslationKey}] does not exist for locale [{activeLocaleID}].");
                            // Leave the $$ prefix and invalid reference in the translated text.
                        }
                    }

                    // Check for any @@ reference in the translated text.
                    else if (translatedText.Contains("@@"))
                    {
                        // Replace @@ references with the regular translated text for the regular translation key.
                        // This works only if the regular translated text is defined before the @@ reference.
                        Translations translationsRegular = 
                            fileLocaleID == DefaultLocaleID ? translationsRegularDefaultLocale : translationsRegularActiveLocale;
                        foreach (string regularTranslationKey in translationsRegular.Keys)
                        {
                            translatedText = translatedText.Replace("@@" + regularTranslationKey, translationsRegular[regularTranslationKey]);
                        }

                        // Replace @@ references with the temporary translated text for the temporary translation key.
                        // This works only if the temporary translated text is defined before the @@ reference.
                        Translations translationsTemporary =
                            fileLocaleID == DefaultLocaleID ? translationsTemporaryDefaultLocale : translationsTemporaryActiveLocale;
                        foreach (string temporaryTranslationKey in translationsTemporary.Keys)
                        {
                            translatedText = translatedText.Replace(temporaryTranslationKey, translationsTemporary[temporaryTranslationKey]);
                        }

                        // Check for invalid @@ reference (i.e. an "@@" reference was not replaced above).
                        if (translatedText.Contains("@@"))
                        {
                            Mod.log.Warn($"Translation for key [{translationKey}] for locale [{activeLocaleID}] has an invalid @@ reference.");
                            // Leave the @@ prefix and invalid reference in the translated text.
                        }
                    }

                    // Set the translation for the default locale.
                    if (fileLocaleID == DefaultLocaleID)
                    {
                        // Set the translation for temporary or regular.
                        Translations translations = isTemporaryKey ? translationsTemporaryDefaultLocale : translationsRegularDefaultLocale;
                        SetTranslation(translations, translationKey, translatedText);
                    }

                    // Set the translation for the active locale.
                    // The active locale can be and often will be the default locale.
                    if (fileLocaleID == activeLocaleID)
                    {
                        // Set the translation for temporary or regular.
                        Translations translations = isTemporaryKey ? translationsTemporaryActiveLocale : translationsRegularActiveLocale;
                        SetTranslation(translations, translationKey, translatedText);
                    }
                }
            }
        }

        /// <summary>
        /// Read a CSV value.
        /// </summary>
        private static string ReadCSVValue(StringReader reader)
        {
            // The value to return
            StringBuilder value = new();

            // Read until non-quoted comma or end-of-string is reached.
            bool inQuotes = false;
            int currentChar = reader.Read();
            while (currentChar != -1)
            {
                // Check for double quote char.
                if (currentChar == '\"')
                {
                    // Check whether or not already in double quotes.
                    if (inQuotes)
                    {
                        // Already in double quotes, check next char.
                        if (reader.Peek() == '\"')
                        {
                            // Next char is double quote.
                            // Consume the second double quote and replace the two consecutive double quotes with one double qoute.
                            reader.Read();
                            value.Append((char)currentChar);
                        }
                        else
                        {
                            // Next char is not double quote.
                            // This double quote is the end of a quoted string, don't append the double quote.
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        // Not already in double quotes.
                        // This double quote is the start of a quoted string, don't append the double quote.
                        inQuotes = true;
                    }
                }
                else
                {
                    // A comma not in double quotes ends the value, don't append the comma.
                    if (currentChar == ',' && !inQuotes)
                    {
                        break;
                    }

                    // All other cases, append the char.
                    value.Append((char)currentChar);
                }

                // Get next char.
                currentChar = reader.Read();
            }

            // Replace any \n with newline.
            string formatted = value.ToString().Replace("\\n", Environment.NewLine);

            // Return the formatted value.
            return formatted;
        }

        /// <summary>
        /// Set the translation for the key and text.
        /// </summary>
        private static void SetTranslation(Translations translations, string translationKey, string translatedText)
        {
            // Check if translation already has some text.
            if (translations[translationKey].Length > 0)
            {
                // Append a newline and then the new translated text.
                translations[translationKey] += Environment.NewLine + translatedText;
            }
            else
            {
                // Set the translated text.
                translations[translationKey] = translatedText;
            }
        }

        /// <summary>
        /// Get the translation of the key using the current active dictionary.
        /// </summary>
        public static string Get(string translationKey)
        {
            // Get the translated text for the translation key.
            if (_localizationManager.activeDictionary.TryGetValue(translationKey, out string translatedText))
            {
                return translatedText;
            }

            // Translation key not found.
            // Return the translation key as the translated text.
            return translationKey;
        }
    }
}
