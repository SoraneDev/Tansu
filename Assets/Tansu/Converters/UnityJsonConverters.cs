using System.Collections.Generic;
using Newtonsoft.Json;


namespace Tansu.Converters
{
    /// <summary>
    /// Unityの基本的な値型を扱うコンバーターの一覧。
    /// </summary>
    public static class UnityJsonConverters
    {
        /// <summary>
        /// すべてのコンバーターの新しいリストを生成する。
        /// </summary>
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
