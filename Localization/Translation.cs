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

        // Translation file format:
        //      Line 1: blank,language code 1,language code 2,...,language code n
        //      Line 2: translation key 1,translated text 1,translated text 2,...,translated text n
        //      Line 3: translation key 2,translated text 1,translated text 2,...,translated text n
        //      ...
        //      Line m: translation key m-1,translated text 1,translated text 2,...,translated text n

        // Translation file notes:
        //      The first line in the file must contain language codes and therefore cannot be blank or a comment.
        //      The file must contain translations for the default language code.
        //      The file should contain translations for every language code supported by the base game.
        //      The file may contain translations for additional language codes.
        //      Language codes in the file may be in any order, except that the default language code must be first.
        //      A language code may not be duplicated.
        //      A blank line is skipped.
        //      A line with a blank translation key is skipped (except the first line).
        //      A line with a translation key that starts with the character (#) is considered a comment and is skipped.
        //      The file should contain a line for every translation key constant name (not value) in UITranslationKey.
        //      Translations keys are case sensitive.
        //      If a translation key is duplicated, then a newline and the translated text are appended to the previous translated text for that key.
        //      Any \n in the translated text is replaced with a newline.
        //      The file must not contain blank columns.
        //      Each language code, translation key, and translated text may or may not be enclosed in double quotes ("text").
        //      Spaces around the comma separators will be included in the translated text.
        //      To include a comma in the translated text, the translated text must be enclosed in double quotes ("te, xt").
        //      To include a double quote in the translated text, use two consecutive double quotes inside the double quoted translated text ("te""xt").
        //      Translated text that contains "@@" will use the translated text from the translation key after the "@@".
        //      The translation key referenced with "@@" must have been read prior to the line with the "@@".
        //      A translation key that starts with "@@" is a temporary translation key that can be referenced in other translated text with "@@".
        //      Translated text cannot be blank for the default language.
        //      Blank translated text in a non-default language will use the translated text for the default language.
        //      Translated text that starts with "$$" will retrieve the game's translated text for whatever key follows the "$$".

        // Default language code.
        private const string DefaultLanguageCode = "en-US";

        // Translations for a single language.
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

        // Translations for all languages in the file.
        // Dictionary key is the language code.
        // Dictionary value contains the translations for that language code.
        private class Languages : Dictionary<string, Translations> { }
        
        /// <summary>
        /// A localization source that can be added to the game.
        /// </summary>
        private class LocalizationSource : IDictionarySource
        {
            // The translations for this localization source.
            private Translations _translations = new Translations();

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

                // Translation keys are the constant names (not values) from UITranslationKey.
                FieldInfo[] fields = typeof(UITranslationKey).GetFields();
                string[] translationKeys = new string[fields.Length];
                for (int i = 0; i < fields.Length; i++)
                {
                    translationKeys[i] = fields[i].Name;
                }

                // Start with only the default language.
                Languages languages = new Languages();
                languages.Add(DefaultLanguageCode, new Translations(translationKeys));

                // Make sure the translation CSV file exists.
                const string translationFile = ModAssemblyInfo.Name + ".Localization.Translation.csv";
                if (!Assembly.GetExecutingAssembly().GetManifestResourceNames().Contains(translationFile))
                {
                    Mod.log.Error($"Translation file [{translationFile}] does not exist in the assembly.");
                    return;
                }

                // Read the text from the translation CSV file.
                string fileText;
                using (Stream fileStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(translationFile))
                {
                    using (StreamReader fileReader = new StreamReader(fileStream, Encoding.UTF8))
                    {
                        fileText = fileReader.ReadToEnd();
                    }
                }

                // Split the text into lines.
                string[] lines = fileText.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.None);

                // First line cannot be blank or a comment.
                string firstLine = lines[0];
                if (firstLine.Trim().Length == 0 || firstLine.StartsWith("#"))
                {
                    Mod.log.Error("Translation file first line is blank or comment. Expecting language codes on the first line.");
                    return;
                }

                // Read language codes from the first line.
                ReadLanguageCodes(firstLine, languages, translationKeys);

                // Create temporary translations for each language code.
                Languages temporaryTranslations = new Languages();
                foreach (string languageCode in languages.Keys)
                {
                    temporaryTranslations.Add(languageCode, new Translations());
                }

                // Initialize count of translation keys.
                Dictionary<string, int> translationKeyCount = new Dictionary<string, int>();
                foreach (string translationKey in translationKeys)
                {
                    translationKeyCount.Add(translationKey, 0);
                }
                
                // Check if file text has any $$ references to game translations.
                Dictionary<string, LocalizationDictionary> gameTranslations = new Dictionary<string, LocalizationDictionary>();
                LocalizationManager localizationManager = GameManager.instance.localizationManager;
                if (fileText.Contains("$$"))
                {
                    // Get game translations once here instead of every time a $$ reference is encountered.
                    string currentLocaleID = localizationManager.activeLocaleId;
                    foreach (string gameLocaleID in localizationManager.GetSupportedLocales())
                    {
                        localizationManager.SetActiveLocale(gameLocaleID);
                        gameTranslations[gameLocaleID] = localizationManager.activeDictionary;
                    }
                    localizationManager.SetActiveLocale(currentLocaleID);
                }

                // Process each subsequent line.
                for (int i = 1; i < lines.Length; i++)
                {
                    ProcessTranslationLine(lines[i], languages, temporaryTranslations, gameTranslations, translationKeys, translationKeyCount);
                }

                // Each translation key must be defined.
                foreach (string translationkey in translationKeys)
                {
                    if (translationKeyCount[translationkey] == 0)
                    {
                        Mod.log.Warn($"Translation key [{translationkey}] is not defined in the translation file.");
                    }
                }

                // Add localization sources to the game.
                // All the translations were read into the languages variable just for this right here.
                foreach (string languageCode in languages.Keys)
                {
                    localizationManager.AddSource(languageCode, new LocalizationSource(languages[languageCode]));
                }
            }
            catch(Exception ex)
            {
                Mod.log.Error(ex);
            }
        }

        /// <summary>
        /// Read languages codes from the first line in the file.
        /// </summary>
        private static void ReadLanguageCodes(string line, Languages languages, string[] translationKeys)
        {
            // Initialize count of language codes.
            Dictionary<string, int> languageCodeCount = new Dictionary<string, int>();
            foreach (string languageCode in languages.Keys)
            {
                languageCodeCount.Add(languageCode, 0);
            };

            // Create a reader on the line.
            using (StringReader reader = new StringReader(line))
            {
                // Read and ignore the first value, which should be blank.
                ReadCSVValue(reader);

                // Read language codes.
                bool firstLanguageCode = true;
                string languageCode = ReadCSVValue(reader);
                while (languageCode.Length != 0)
                {
                    // Check if language code already exists.
                    if (languages.ContainsKey(languageCode))
                    {
                        // Count language code occurrences.
                        languageCodeCount[languageCode]++;
                    }
                    else
                    {
                        // Add the new language code with an initial count of 1.
                        languages.Add(languageCode, new Translations(translationKeys));
                        languageCodeCount.Add(languageCode, 1);
                    }

                    // Check that the first language code in the file is the default language code.
                    if (firstLanguageCode && languageCode != DefaultLanguageCode)
                    {
                        Mod.log.Warn($"Translation file must have default language code [{DefaultLanguageCode}] defined first.");
                    }

                    // Get next language code.
                    languageCode = ReadCSVValue(reader);
                    firstLanguageCode = false;
                }
            }

            // Each language code must be defined exactly once.
            foreach (string languageCode in languageCodeCount.Keys)
            {
                if (languageCodeCount[languageCode] != 1)
                {
                    Mod.log.Warn($"Translation file defines language code [{languageCode}] {languageCodeCount[languageCode]} times.  Expecting 1 time.");
                }
            }
        }

        /// <summary>
        /// Process a line from the translation file.
        /// </summary>
        private static void ProcessTranslationLine
        (
            string line,
            Languages languages,
            Languages temporaryTranslations,
            Dictionary<string, LocalizationDictionary> gameTranslations,
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
            using (StringReader reader = new StringReader(line))
            {
                // The first value in the line is the translation key.
                string translationKey = ReadCSVValue(reader);

                // Skip lines with a blank or comment translation key.
                if (translationKey.Length == 0 || translationKey.StartsWith("#"))
                {
                    return;
                }

                // Check for temporary key.
                bool temporaryKey = translationKey.StartsWith("@@");
                if (temporaryKey)
                {
                    // Check if temporary key does not already exist.
                    if (!temporaryTranslations[DefaultLanguageCode].ContainsKey(translationKey))
                    {
                        // For each language code, add the temporary key to the temporary translations.
                        foreach (string languageCode in languages.Keys)
                        {
                            temporaryTranslations[languageCode].Add(translationKey, "");
                        }
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

                // Do each language code.
                foreach (string languageCode in languages.Keys)
                {
                    // Get the translated text.
                    string translatedText = ReadCSVValue(reader);

                    // Check for blank translated text.
                    if (string.IsNullOrEmpty(translatedText))
                    {
                        // Check for default language.
                        if (languageCode == DefaultLanguageCode)
                        {
                            // For default language, warn and use the key as the translated text.
                            Mod.log.Warn($"Translation for key [{translationKey}] must be defined for default language code [{DefaultLanguageCode}].");
                            translatedText = translationKey;
                        }
                        else
                        {
                            // Other than default language.

                            // Check for temporary key.
                            if (temporaryKey)
                            {
                                // Use temporary translated text from default language.
                                translatedText = temporaryTranslations[DefaultLanguageCode][translationKey];
                            }
                            else
                            {
                                // Use translated text from default language.
                                translatedText = languages[DefaultLanguageCode][translationKey];
                            }
                        }
                    }

                    // Check for $$ reference in the translated text.
                    if (translatedText.StartsWith("$$"))
                    {
                        // Get game translation key after the $$.
                        string gameTranslationKey = translatedText.Substring(2);

                        // Get the game's translation for the key.
                        if (gameTranslations.ContainsKey(languageCode))
                        {
                            if (gameTranslations[languageCode].TryGetValue(gameTranslationKey, out string gameTranslatedText))
                            {
                                // Use the game translated text.
                                translatedText = gameTranslatedText;
                            }
                            else
                            {
                                Mod.log.Warn($"Game translation key [{gameTranslationKey}] does not exist for language [{languageCode}].");
                                // Leave the invalid $$ reference in the translated text.
                            }
                        }
                        else
                        {
                            Mod.log.Warn($"Game does not contain translations for language [{languageCode}] for translation key [{gameTranslationKey}].");
                            // Leave the invalid $$ reference in the translated text.
                        }
                    }

                    // Check for any @@ reference in the translated text.
                    else if (translatedText.Contains("@@"))
                    {
                        // Do each existing translation key.
                        foreach (string existingTranslationkey in translationKeys)
                        {
                            // Replace the @@ reference with the existing translated text for the existing translation key.
                            // This works only if the existing translated text is defined before the @@ reference.
                            translatedText = translatedText.Replace("@@" + existingTranslationkey, languages[languageCode][existingTranslationkey]);
                        }

                        // Do each temporary translation key.
                        foreach (string temporaryTranslationkey in temporaryTranslations[DefaultLanguageCode].Keys)
                        {
                            // Replace the @@ reference with the temporary translated text for the temporary translation key.
                            // This works only if the temporary translated text is defined before the @@ reference.
                            translatedText = translatedText.Replace(temporaryTranslationkey, temporaryTranslations[languageCode][temporaryTranslationkey]);
                        }

                        // Check for invalid @@ reference (i.e. an "@@" reference was not replaced above).
                        if (translatedText.Contains("@@"))
                        {
                            Mod.log.Warn($"Translation for key [{translationKey}] for language [{languageCode}] has an invalid @@ reference.");
                            // Leave the invalid @@ reference in the translated text.
                        }
                    }

                    // Check if updating temporary or regular translations.
                    if (temporaryKey)
                    {
                        // Check if already have translated text.
                        if (temporaryTranslations[languageCode][translationKey].Length > 0)
                        {
                            // Append a newline and then the new translated text.
                            temporaryTranslations[languageCode][translationKey] += Environment.NewLine + translatedText;
                        }
                        else
                        {
                            // Save the translated text.
                            temporaryTranslations[languageCode][translationKey] = translatedText;
                        }
                    }
                    else
                    {
                        // Check if already have translated text.
                        if (languages[languageCode][translationKey].Length > 0)
                        {
                            // Append a newline and then the new translated text.
                            languages[languageCode][translationKey] += Environment.NewLine + translatedText;
                        }
                        else
                        {
                            // Save the translated text.
                            languages[languageCode][translationKey] = translatedText;
                        }
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
            StringBuilder value = new StringBuilder();

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
        /// Get the translation of the key using the current active language code.
        /// </summary>
        public static string Get(string translationKey)
        {
            return Get(translationKey, GameManager.instance.localizationManager.activeLocaleId);
        }

        /// <summary>
        /// Get the translation of the key using the specified language code.
        /// </summary>
        public static string Get(string translationKey, string languageCode)
        {
            // If language code is not supported, then use default language code.
            // This can happen if a language in the base game is not defined in the translation file.
            // This can happen if a mod adds a language to the game and that language is not defined in the translation file.
            LocalizationManager localizationManager = GameManager.instance.localizationManager;
            if (!localizationManager.SupportsLocale(languageCode))
            {
                languageCode = DefaultLanguageCode;
            }

            // Get the translated text for the translation key.
            if (localizationManager.activeDictionary.TryGetValue(translationKey, out string translatedText))
            {
                return translatedText;
            }

            // Translation key not found.
            // Return the translation key as the translated text.
            return translationKey;
        }
    }
}
