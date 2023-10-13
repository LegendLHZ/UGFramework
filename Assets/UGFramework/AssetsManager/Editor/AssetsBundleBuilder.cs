using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UGFramework.AssetsManager.Editor
{
    public class AssetsBundleBuilder
    {
        private static readonly string HashPath = "Assets/Editor/AssetsHashData.asset";
        private static readonly string ResourcePath = "Assets/BundleRes";
        private static readonly string OutputPath = "Assets/AssetsBundle";

        [MenuItem("AssetsBundle/Build")]
        public static void Build()
        {
            if (!Directory.Exists(OutputPath))
            {
                Directory.CreateDirectory(OutputPath);
            }
            BuildPipeline.BuildAssetBundles(OutputPath, GetBundles().ToArray(), BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);            
        }

        public static List<AssetBundleBuild> GetBundles()
        {
            var assetsHashData = AssetDatabase.LoadAssetAtPath<AssetsHashData>(HashPath);
            if (assetsHashData == null)
            {
                var dir = HashPath.Substring(0, HashPath.LastIndexOf('/'));
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                assetsHashData = ScriptableObject.CreateInstance<AssetsHashData>();
                AssetDatabase.CreateAsset(assetsHashData, HashPath);
            }

            List<AssetBundleBuild> ret = new List<AssetBundleBuild>();
            void GetChangedBundles(string path, List<AssetBundleBuild> list)
            {
                var allFiles = AssetDatabase.FindAssets(path);
                if (FilesIsChange(allFiles, assetsHashData))
                {
                    AssetBundleBuild build = new AssetBundleBuild();
                    var lastIndex = path.LastIndexOf('/');
                    build.assetBundleName = lastIndex == -1 ? path : path.Substring(lastIndex + 1);
                    build.assetNames = new string[allFiles.Length];
                    for (int i = 0; i < allFiles.Length; i++)
                    {
                        build.assetNames[i] = allFiles[i];
                    }
                    list.Add(build);
                }
                
                foreach (var directory in Directory.GetDirectories(path))
                {
                    GetChangedBundles(directory, list);
                }
            }
            
            GetChangedBundles(ResourcePath, ret);

            return ret;
        }

        public static bool FilesIsChange(string[] files, AssetsHashData hashData)
        {
            foreach (var path in files)
            {
                var file = new FileStream(path, FileMode.Open);
                if (!hashData.assetsHashData.TryGetValue(path, out var oldHash))
                {
                    return true;
                }
                SHA1 sha1 = new SHA1CryptoServiceProvider(); 
                var hash = sha1.ComputeHash(file);
                file.Close();
                var sc = new StringBuilder();
                foreach (var bt in hash)
                {
                    sc.Append(bt.ToString("x2"));
                }

                if (sc.ToString() != oldHash)
                {
                    return true;
                }
            }

            return false;
        }
    }
}