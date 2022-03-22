
using UnityEngine;

namespace Nessie.Utilities.Package
{
    [CreateAssetMenu(fileName = "Package_Data", menuName = "ScriptableObjects/Package Builder/Data", order = 1)]
    public class PackageData : ScriptableObject
    {
        public UnityEditor.ExportPackageOptions Flags;
        public string PackageName = "";
        public string PackagePath = "Assets/Editor/Packages/";
        public string[] AssetPaths = new string[0];
    }
}