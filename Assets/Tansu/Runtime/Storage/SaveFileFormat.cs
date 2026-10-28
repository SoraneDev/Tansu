using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace Tansu
{
    /// <summary>
    /// 保存ファイルの中身（JSON）の作成と解析を行う。
    /// </summary>
    /// <remarks>
    /// 中身は次の形になる。"version"はファイル形式のバージョンで、将来形式を変えたときに古い形式と見分けるために使う。
    /// <code>
    /// {
    ///   "version": 1,
    ///   "values": {
    ///     "player.coins": 120
    ///   }
    /// }
    /// </code>
    /// </remarks>
    internal static class SaveFileFormat
    {
        // このアプリが書き込むファイル形式のバージョン。形式を変えたら1つ上げる。
        private const int CurrentVersion = 1;
        private const string VersionKey = "version";
        private const string ValuesKey = "values";


        /// <summary>
        /// 値の一覧から、ファイルに書き込むテキストを作る。
        /// </summary>
        /// <param name="values">キー名ごとの値の一覧。</param>
        /// <returns>ファイルに書き込むテキストを返す。</returns>
        public static string CreateText(JObject values)
        {
            using var stringWriter = new StringWriter(CultureInfo.InvariantCulture);
            using (var writer = new JsonTextWriter(stringWriter))
            {
                writer.Formatting = Formatting.Indented;
                writer.WriteStartObject();

                writer.WritePropertyName(VersionKey);
                writer.WriteValue(CurrentVersion);

                writer.WritePropertyName(ValuesKey);
                values.WriteTo(writer);

                writer.WriteEndObject();
            }

            return stringWriter.ToString();
        }


        /// <summary>
        /// ファイルのテキストを解析し、値の一覧を取り出す。
        /// </summary>
        /// <param name="text">ファイルの中身。</param>
        /// <param name="values">取り出した値の一覧。解析に成功しなかった場合はnull。</param>
        /// <returns>ファイルの状態を返す。</returns>
        public static SaveFileState Parse(string text, out JObject values)
        {
            values = null;

            JObject root;
            try
            {
                using var reader = new JsonTextReader(new StringReader(text));

                // 既定では日付らしい文字列が自動でDateTimeに変換され、書き戻したときに表記が変わってしまう。
                // 保存した文字列をそのまま保つため、変換しない設定にする。
                reader.DateParseHandling = DateParseHandling.None;

                root = JObject.Load(reader);
            }
            catch (JsonReaderException)
            {
                // 途中で切れている、JSONとして壊れている、一番外側がオブジェクトでないなど。
                return SaveFileState.Broken;
            }

            if (root[VersionKey] is not JValue version || version.Type != JTokenType.Integer)
            {
                return SaveFileState.Broken;
            }

            // longに収まらないほど大きな値は、新しい形式とも判断できないため、壊れているものとして扱う。
            if (version.Value is not long versionNumber)
            {
                return SaveFileState.Broken;
            }

            if (versionNumber > CurrentVersion)
            {
                return SaveFileState.TooNew;
            }

            if (root[ValuesKey] is not JObject parsedValues)
            {
                return SaveFileState.Broken;
            }

            values = parsedValues;
            return SaveFileState.Valid;
        }
    }
}
