using Colossal.Localization;
using Game.SceneFlow;
using Game.Settings;

namespace ChangeCompany
{
    /// <summary>
    /// A game date and time.
    /// </summary>
    public class GameDateTime
    {
        // The date/time parameters that the game uses.
        public int Year;
        public int Month = 1;   // Game month is from 1 to 12.
        public int Hour;
        public int Minute;

        // Hold a reference to localization manager.
        private static readonly LocalizationManager _localizationManager = GameManager.instance.localizationManager;

        /// <summary>
        /// Return a copy of this game date/time.
        /// </summary>
        public GameDateTime Copy()
        {
            return new GameDateTime
            {
                Year    = this.Year,
                Month   = this.Month,
                Hour    = this.Hour,
                Minute  = this.Minute
            };
        }

        /// <summary>
        /// Copy some other GameDateTime into this one.
        /// </summary>
        public void CopyFrom(GameDateTime other)
        {
            Year    = other.Year;
            Month   = other.Month;
            Hour    = other.Hour;
            Minute  = other.Minute;
        }

        /// <summary>
        /// Format this game date/time for display in the UI.
        /// Format matches the game's main display of game date/time.
        /// The game uses other date/time formats elsewhere.
        /// </summary>
        public string FormatForUI()
        {
            // Check for no date/time.
            if (Year == 0)
            {
                return Translation.Get("Common.NONE");
            }

            // Get the time format (12 vs 24 hour) from game settings.
            InterfaceSettings interfaceSettings = GameManager.instance?.settings?.userInterface;
            InterfaceSettings.TimeFormat timeFormat =
                (interfaceSettings != null ? interfaceSettings.timeFormat : InterfaceSettings.TimeFormat.TwentyFourHours);

            // For 12-hour format, convert the hours and get AM/PM.
            int convertedHour = Hour;
            string amPM = "";
            if (timeFormat == InterfaceSettings.TimeFormat.TwelveHours)
            {
                // Converted hours are from 0 to 11, except 0 is replaced with 12.
                // So:  12, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11
                convertedHour = Hour % 12;
                if (convertedHour == 0)
                {
                    convertedHour = 12;
                }

                // Get AM vs PM.
                // Game's translation keys for formatting AM and PM:
                //      Common.TIME_PERIOD_AM = AM
                //      Common.TIME_PERIOD_PM = PM
                amPM = " " + Translation.Get("Common.TIME_PERIOD_" + (Hour < 12 ? "AM" : "PM"));
            }

            // Construct formatted time.
            // The game always uses 2 digits for the hour, even for 12-hour format.
            // All languages use a colon for the time separator.
            string formattedTime = convertedHour.ToString("00") + ":" + Minute.ToString("00") + amPM;

            // Japanese, Korean, and Chinese need special date handling.
            string activeLocaleId = _localizationManager.activeLocaleId;
            bool specialDateHandling = 
                activeLocaleId == "ja-JP"   ||
                activeLocaleId == "ko-KR"   ||
                activeLocaleId == "zh-HANS" ||
                activeLocaleId == "zh-HANT";

            // Get formatted month.
            // Special formatted month has month number 1-12 followed by month label.
            // Normal  formatted month has month translation.
            // Game month is from 1-12, but translation key is from 0-11.
            // Game's translation keys for formatting month:
            //      Common.MONTH_SHORT:0  = Jan
            //      Common.MONTH_SHORT:1  = Feb
            //      ...
            //      Common.MONTH_SHORT:10 = Nov
            //      Common.MONTH_SHORT:11 = Dec
            string formattedMonth = specialDateHandling ?
                Month.ToString() + Translation.Get("Paradox.BIRTH_DATE_MONTH_FIELD_LABEL") :
                Translation.Get("Common.MONTH_SHORT:" + (Month - 1));

            // Get formatted year.
            // All formats have the year number.
            // Special formatted year has year number followed by year label.
            string formattedYear = Year.ToString("0000") +
                (specialDateHandling ? Translation.Get("Paradox.BIRTH_DATE_YEAR_FIELD_LABEL") : "");

            // Construct formatted date.
            // Special formatted date has year  then month.
            // Normal  formatted date has month then year.
            string formattedDate = specialDateHandling ?
                formattedYear  + " " + formattedMonth :
                formattedMonth + " " + formattedYear;

            // Return the formatted game date/time.
            // Game always has time before date in its main display.
            return formattedTime + " " + formattedDate;
        }
    }
}
