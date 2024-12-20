using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Stovepipe.Debug
{
    public static class DebugIO
    {
        private static readonly JsonSerializerSettings IgnoreSelfReference = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        public static void WriteNewAdjustment(string nameOfGun, StovepipeAdjustment adjustments)
        {
            if (UserConfig.IsWriteToDefault.Value)
                WriteOrReplaceInDict(nameOfGun, adjustments, UserConfig.DefaultStovepipeAdjustments, UserConfig.DefaultStovepipeDir);
            else
                WriteOrReplaceInDict(nameOfGun, adjustments, UserConfig.UserDefinedStovepipeAdjustments, UserConfig.UserDefStovepipeDir);
        }

        public static void WriteNewAdjustment(string nameOfGun, DoubleFeedAdjustment adjustments)
        {
            if (UserConfig.IsWriteToDefault.Value)
                WriteOrReplaceInDict(nameOfGun, adjustments, UserConfig.DefaultDoubleFeedAdjustments, UserConfig.DefaultDoubleFeedDir);
            else
                WriteOrReplaceInDict(nameOfGun, adjustments, UserConfig.UserDefinedDoubleFeedAdjustments, UserConfig.UserDefDoubleFeedDir);
        }

        private static void WriteOrReplaceInDict(string nameOfGun, StovepipeAdjustment adjustment,
            IDictionary<string, StovepipeAdjustment> dict, string dictDir)
        {
            if (dict.TryGetValue(nameOfGun, out _)) dict.Remove(nameOfGun);
            dict.Add(nameOfGun, adjustment);
            File.WriteAllText(dictDir, JsonConvert.SerializeObject(dict, Formatting.Indented, IgnoreSelfReference));
        }

        private static void WriteOrReplaceInDict(string nameOfGun, DoubleFeedAdjustment adjustment,
            IDictionary<string, DoubleFeedAdjustment> dict, string dictDir)
        {
            if (dict.TryGetValue(nameOfGun, out _)) dict.Remove(nameOfGun);
            dict.Add(nameOfGun, adjustment);
            File.WriteAllText(dictDir, JsonConvert.SerializeObject(dict, Formatting.Indented, IgnoreSelfReference));
        }

        public static StovepipeAdjustment ReadStovepipeAdjustment(string rawNameOfGun)
        {
            var cleanedName = rawNameOfGun.Remove(rawNameOfGun.Length - 7);

            if (UserConfig.UserDefinedStovepipeAdjustments.TryGetValue(cleanedName, out var adjustment)) return adjustment;

            UserConfig.DefaultStovepipeAdjustments.TryGetValue(cleanedName, out adjustment);

            return adjustment;
        }

        public static DoubleFeedAdjustment ReadDoubleFeedAdjustment(string rawNameOfGun)
        {
            var cleanedName = rawNameOfGun.Remove(rawNameOfGun.Length - 7);

            if (UserConfig.UserDefinedDoubleFeedAdjustments.TryGetValue(cleanedName, out var adjustment)) return adjustment;

            UserConfig.DefaultDoubleFeedAdjustments.TryGetValue(cleanedName, out adjustment);

            return adjustment;
        }
    }
}