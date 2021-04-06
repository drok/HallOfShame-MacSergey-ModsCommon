﻿using System;
using System.Collections.Generic;
using System.Text;

namespace ModsCommon
{
    public abstract class SingletonItem<T>

    {
        public static T Instance { get; set; }
    }
    public abstract class SingletonMod<T> : SingletonItem<T>
        where T : BaseMod<T>
    {
        public static string Name => Instance.Name;
        public static string NameRaw => Instance.NameRaw;
        public static Logger Logger => Instance.Logger;
        public static Version Version => Instance.Version;
        public static List<Version> Versions => Instance.Versions;
        public static string VersionString => Instance.VersionString;
        public static string Id => Instance.Id;
        public static bool IsBeta => Instance.IsBeta;
    }
    public abstract class SingletonTool<T> : SingletonItem<T> where T : BaseTool<T> { }
}