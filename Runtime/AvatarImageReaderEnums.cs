﻿using UnityEngine;

namespace AvatarImageReader.Enums
{
    public enum Platform
    {
        Android,
        PC
    }

    public enum DataMode
    {
        UTF8,
        UTF16,
        [InspectorName("ASCII (Not supported yet)")]
        ASCII,
        [InspectorName("Binary (Not supported yet)")]
        Binary
    }
}
