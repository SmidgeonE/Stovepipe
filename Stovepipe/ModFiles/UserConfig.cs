using System.Collections.Generic;
using BepInEx.Configuration;

namespace Stovepipe
{
    public static class UserConfig
    {
        public static string DefaultStovepipeDir;
        public static string UserDefStovepipeDir;

        public static string DefaultDoubleFeedDir;
        public static string UserDefDoubleFeedDir;
        
        public static ConfigEntry<bool> IsDebug;
        public static ConfigEntry<bool> IsWriteToDefault;
        public static ConfigEntry<bool> IsDoubleFeedDebugMode;

        public static ConfigEntry<bool> IsStovepipeEnabled;
        public static ConfigEntry<bool> IsDoubleFeedEnabled;
        public static ConfigEntry<bool> IsBatteryFailureEnabled;

        public static ConfigEntry<float> BatteryFailureProb;

        public static ConfigEntry<float> StovepipeHandgunProb;
        public static ConfigEntry<float> StovepipeRifleProb;
        public static ConfigEntry<float> StovepipeTubeFedProb;
        public static ConfigEntry<float> StovepipeOpenBoltProb;

        public static ConfigEntry<float> DoubleFeedHandgunProb;
        public static ConfigEntry<float> DoubleFeedRifleProb;
        public static ConfigEntry<float> LowerBulletDropoutProb;
        public static ConfigEntry<float> UpperBulletDropoutProb;
        public static ConfigEntry<float> LowerBulletShakeyProb;
        public static ConfigEntry<float> UpperBulletShakeyProb;

        public static ConfigEntry<int> MinRoundBeforeNextJam;
        public static ConfigEntry<bool> UseProbabilityCreep;
        public static ConfigEntry<int> ProbabilityCreepNumRounds;

        public static Dictionary<string, StovepipeAdjustment> DefaultStovepipeAdjustments;
        public static Dictionary<string, StovepipeAdjustment> UserDefinedStovepipeAdjustments;

        public static Dictionary<string, DoubleFeedAdjustment> DefaultDoubleFeedAdjustments;
        public static Dictionary<string, DoubleFeedAdjustment> UserDefinedDoubleFeedAdjustments;
    }
}