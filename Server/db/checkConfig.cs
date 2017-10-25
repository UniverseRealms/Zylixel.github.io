﻿#region

using db;

#endregion

namespace db
{
    public class CheckConfig
    {
        private static readonly SimpleSettings Settings = new SimpleSettings("wServer");

        public static bool IsDebugOn()
        {
            return Settings.GetValue<bool>("debugMode", "false");
        }
    }
}