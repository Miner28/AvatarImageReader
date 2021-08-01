using UnityEngine;
using System;
using System.Linq;

namespace VRC.Udon
{
    public static class UdonNetworkTypes
    {
        public static bool CanSync(System.Type type) => SyncTypes.Contains(type);
        public static bool CanSyncLinear(System.Type type) => LinearTypes.Contains(type);
        public static bool CanSyncSmooth(System.Type type) => SmoothTypes.Contains(type);

        private static Type[] SyncTypes = new Type[] {
            typeof(bool),
            typeof(char),
            typeof(byte),
            typeof(int),
            typeof(long),
            typeof(sbyte),
            typeof(ulong),
            typeof(float),
            typeof(double),
            typeof(short),
            typeof(ushort),
            typeof(string),
            typeof(bool[]),
            typeof(char[]),
            typeof(byte[]),
            typeof(int[]),
            typeof(long[]),
            typeof(sbyte[]),
            typeof(ulong[]),
            typeof(float[]),
            typeof(double[]),
            typeof(short[]),
            typeof(ushort[]),
            typeof(Color),
            typeof(Color32),
            typeof(Vector2),
            typeof(Vector3),
            typeof(Vector4),
            typeof(Quaternion),
            typeof(Vector2[]),
            typeof(Vector3[]),
            typeof(Vector4[]),
            typeof(Quaternion[]),
            typeof(Color[]),
            typeof(Color32[]),
            typeof(SDKBase.VRCUrl),
            typeof(SDKBase.VRCUrl[]),
        };

        private static Type[] LinearTypes = new Type[] {
            typeof(byte),
            typeof(sbyte),
            typeof(short),
            typeof(ushort),
            typeof(uint),
            typeof(int),
            typeof(ulong),
            typeof(long),
            typeof(float),
            typeof(double),
            typeof(Vector2),
            typeof(Vector3),
            typeof(Quaternion),
            typeof(Color),
            typeof(Color32),
        };

        private static Type[] SmoothTypes = new Type[] {
            typeof(byte),
            typeof(sbyte),
            typeof(short),
            typeof(ushort),
            typeof(uint),
            typeof(int),
            typeof(ulong),
            typeof(long),
            typeof(float),
            typeof(double),
            typeof(Vector2),
            typeof(Vector3),
            typeof(Quaternion),
        };
    }
}