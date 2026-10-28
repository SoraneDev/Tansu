using System.Collections.Generic;
using Newtonsoft.Json;


namespace Tansu
{
    /// <summary>
    /// Unityの基本的な値型を扱うコンバーターを提供する。
    /// </summary>
    internal static class UnityJsonConverters
    {
        /// <summary>
        /// すべてのコンバーターを、新しいリストにまとめて生成する。
        /// </summary>
        /// <returns>生成したコンバーターのリストを返す。</returns>
        public static List<JsonConverter> CreateAll()
        {
            return new List<JsonConverter>
            {
                new Vector2Converter(),
                new Vector3Converter(),
                new Vector4Converter(),
                new Vector2IntConverter(),
                new Vector3IntConverter(),
                new QuaternionConverter(),
                new ColorConverter(),
                new Color32Converter(),
            };
        }
    }
}
