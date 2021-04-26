﻿using ColossalFramework;
using ColossalFramework.UI;
using HarmonyLib;
using ICities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Linq;

namespace ModsCommon.Utilities
{
    public abstract class BaseAssetDataExtension<TypeExtension> : AssetDataExtensionBase
        where TypeExtension : BaseAssetDataExtension<TypeExtension>
    {
        public static void LoadAssetPanelOnLoadPostfix(LoadAssetPanel __instance, UIListBox ___m_SaveList)
        {
            if (AccessTools.Method(typeof(LoadSavePanelBase<CustomAssetMetaData>), "GetListingMetaData") is not MethodInfo method)
                return;

            var listingMetaData = (CustomAssetMetaData)method.Invoke(__instance, new object[] { ___m_SaveList.selectedIndex });
            if (listingMetaData.userDataRef != null)
            {
                var userAssetData = (listingMetaData.userDataRef.Instantiate() as AssetDataWrapper.UserAssetData) ?? new AssetDataWrapper.UserAssetData();
                SingletonItem<TypeExtension>.Instance.OnAssetLoaded(listingMetaData.name, ToolsModifierControl.toolController.m_editPrefabInfo, userAssetData.Data);
            }
        }
    }

    public abstract class BaseAssetDataExtension<TypeExtension, TypeAssetData> : BaseAssetDataExtension<TypeExtension>
        where TypeExtension : BaseAssetDataExtension<TypeExtension, TypeAssetData>
    {
        protected Dictionary<BuildingInfo, TypeAssetData> AssetDatas { get; } = new Dictionary<BuildingInfo, TypeAssetData>();

        public override void OnCreated(IAssetData assetData)
        {
            base.OnCreated(assetData);
            SingletonItem<TypeExtension>.Instance = (TypeExtension)this;
        }
        public override void OnReleased() => SingletonItem<TypeExtension>.Instance = null;
        public override void OnAssetLoaded(string name, object asset, Dictionary<string, byte[]> userData)
        {
            if(asset is BuildingInfo prefab && userData != null && Load(prefab, userData, out var data))
                AssetDatas[prefab] = data;
        }
        public override void OnAssetSaved(string name, object asset, out Dictionary<string, byte[]> userData)
        {
            userData = new Dictionary<string, byte[]>();

            if (asset is BuildingInfo prefab && prefab.m_paths.Any())
                Save(prefab, userData);
        }

        public abstract bool Load(BuildingInfo prefab, Dictionary<string, byte[]> userData, out TypeAssetData data);
        public abstract void Save(BuildingInfo prefab, Dictionary<string, byte[]> userData);
    }

    public abstract class BaseIntersectionAssetDataExtension<TypeMod, TypeExtension> : BaseAssetDataExtension<TypeExtension, AssetData>
        where TypeMod : BaseMod<TypeMod>
        where TypeExtension : BaseIntersectionAssetDataExtension<TypeMod, TypeExtension>
    {
        protected abstract string DataId { get; }
        protected abstract string MapId { get; }

        public override bool Load(BuildingInfo prefab, Dictionary<string, byte[]> userData, out AssetData data)
        {
            if (userData.TryGetValue(DataId, out byte[] rawData) && userData.TryGetValue(MapId, out byte[] map))
            {
                SingletonMod<TypeMod>.Logger.Debug($"Start load prefab data \"{prefab.name}\"");
                try
                {
                    var decompress = Loader.Decompress(rawData);
                    var config = XmlExtension.Parse(decompress);

                    SetMap(map, out var segments, out var nodes);
                    data = new AssetData(config, segments, nodes);
                    SingletonMod<TypeMod>.Logger.Debug($"Prefab data was loaded; Size = {rawData.Length} bytes");
                    return true;
                }
                catch (Exception error)
                {
                    SingletonMod<TypeMod>.Logger.Error("Could not load prefab data", error);
                }
            }

            data = default;
            return false;
        }
        public override void Save(BuildingInfo prefab, Dictionary<string, byte[]> userData)
        {
            SingletonMod<TypeMod>.Logger.Debug($"Start save prefab data \"{prefab.name}\"");
            try
            {
                var config = Loader.GetString(GetConfig());
                var data = Loader.Compress(config);

                userData[DataId] = data;
                userData[MapId] = GetMap();

                SingletonMod<TypeMod>.Logger.Debug($"Prefab data was saved; Size = {data.Length} bytes");
            }
            catch (Exception error)
            {
                SingletonMod<TypeMod>.Logger.Error("Could not save prefab data", error);
            }
        }

        public void OnPlaceAsset(BuildingInfo buildingInfo, FastList<ushort> segments, FastList<ushort> nodes)
        {
            if (AssetDatas.TryGetValue(buildingInfo, out var assetData))
                PlaceAsset(assetData, segments, nodes);
        }

        protected abstract void PlaceAsset(AssetData data, FastList<ushort> segments, FastList<ushort> nodes);

        protected abstract XElement GetConfig();

        private byte[] GetMap()
        {
            var instance = Singleton<NetManager>.instance;

            var segmentsId = new List<ushort>();
            for (ushort i = 0; i < NetManager.MAX_SEGMENT_COUNT; i += 1)
            {
                if (instance.m_segments.m_buffer[i].m_flags.CheckFlags(NetSegment.Flags.Created))
                    segmentsId.Add(i);
            }

            var map = new byte[sizeof(ushort) * 3 * segmentsId.Count];

            for (var i = 0; i < segmentsId.Count; i += 1)
            {
                var segmentId = segmentsId[i];
                var segment = instance.m_segments.m_buffer[segmentId];
                GetBytes(segmentId, out map[i * 6], out map[i * 6 + 1]);
                GetBytes(segment.m_startNode, out map[i * 6 + 2], out map[i * 6 + 3]);
                GetBytes(segment.m_endNode, out map[i * 6 + 4], out map[i * 6 + 5]);
            }

            return map;
        }
        private void SetMap(byte[] map, out ushort[] segments, out ushort[] nodes)
        {
            var count = map.Length / 6;
            segments = new ushort[count];
            nodes = new ushort[count * 2];

            for (var i = 0; i < count; i += 1)
            {
                segments[i] = GetUShort(map[i * 6], map[i * 6 + 1]);
                nodes[i * 2] = GetUShort(map[i * 6 + 2], map[i * 6 + 3]);
                nodes[i * 2 + 1] = GetUShort(map[i * 6 + 4], map[i * 6 + 5]);
            }
        }
        private void GetBytes(ushort n, out byte b1, out byte b2)
        {
            b1 = (byte)(n >> 8);
            b2 = (byte)n;
        }
        private ushort GetUShort(byte b1, byte b2) => (ushort)((b1 << 8) + b2);

        public static IEnumerable<CodeInstruction> BuildingDecorationLoadPathsTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var segmentBufferField = AccessTools.DeclaredField(typeof(NetManager), nameof(NetManager.m_tempSegmentBuffer));
            var nodeBufferField = AccessTools.DeclaredField(typeof(NetManager), nameof(NetManager.m_tempNodeBuffer));
            var clearMethod = AccessTools.DeclaredMethod(nodeBufferField.FieldType, nameof(FastList<ushort>.Clear));

            var matchCount = 0;
            var inserted = false;
            var enumerator = instructions.GetEnumerator();
            var prevInstruction = (CodeInstruction)null;
            while (enumerator.MoveNext())
            {
                var instruction = enumerator.Current;

                if (prevInstruction != null && prevInstruction.opcode == OpCodes.Ldfld && prevInstruction.operand == nodeBufferField && instruction.opcode == OpCodes.Callvirt && instruction.operand == clearMethod)
                    matchCount += 1;

                if (!inserted && matchCount == 2)
                {
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(SingletonItem<TypeExtension>), nameof(SingletonItem<TypeExtension>.Instance)));
                    yield return new CodeInstruction(OpCodes.Box, typeof(TypeExtension));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldloc_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, segmentBufferField);
                    yield return new CodeInstruction(OpCodes.Ldloc_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, nodeBufferField);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TypeExtension), nameof(OnPlaceAsset)));
                    inserted = true;
                }

                if (prevInstruction != null)
                    yield return prevInstruction;

                prevInstruction = instruction;
            }

            if (prevInstruction != null)
                yield return prevInstruction;
        }
    }
    public struct AssetData
    {
        public XElement Config { get; }
        public ushort[] Segments { get; }
        public ushort[] Nodes { get; }

        public AssetData(XElement config, ushort[] segments, ushort[] nodes)
        {
            Config = config;
            Segments = segments;
            Nodes = nodes;
        }
    }
}
