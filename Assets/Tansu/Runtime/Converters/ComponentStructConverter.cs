using System;
using System.Globalization;
using Newtonsoft.Json;


namespace Tansu
{
    /// <summary>
    /// 数値の成分だけで構成される構造体（<see cref="UnityEngine.Vector3" />、<see cref="UnityEngine.Color" />など）を、
    /// <c>{"x":1,"y":2}</c>の形式で読み書きする。
    /// </summary>
    /// <typeparam name="T">対象の構造体。</typeparam>
    /// <remarks>
    /// 読み込み時、JSONに存在しない成分は生成時に指定した既定値になり、未知のプロパティは読み飛ばす。
    /// 成分は内部でdoubleとして扱う。floatとintのどちらも誤差なく表現できるため。
    /// </remarks>
    internal abstract class ComponentStructConverter<T> : JsonConverter<T>
        where T : struct
    {
        private readonly string[] _componentNames;
        private readonly double[] _defaultComponents;


        /// <summary>
        /// 成分名と、JSONに存在しない成分の既定値を指定して作成する。
        /// </summary>
        /// <param name="componentNames">JSON上の成分名。並び順が成分の順番になる。</param>
        /// <param name="defaultComponents">
        /// JSONに存在しない成分の既定値。<paramref name="componentNames" />と同じ長さ。
        /// </param>
        /// <exception cref="ArgumentException">2つの配列の長さが異なる場合。</exception>
        protected ComponentStructConverter(string[] componentNames, double[] defaultComponents)
        {
            if (componentNames.Length != defaultComponents.Length)
            {
                throw new ArgumentException(
                    $"{nameof(componentNames)} と {nameof(defaultComponents)} の長さが異なります。");
            }

            _componentNames = componentNames;
            _defaultComponents = defaultComponents;
        }


        /// <summary>成分を整数として書き出すかどうか。</summary>
        protected virtual bool WritesIntegers => false;


        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, T value, JsonSerializer serializer)
        {
            Span<double> components = stackalloc double[_componentNames.Length];
            Decompose(value, components);

            writer.WriteStartObject();

            for (int i = 0; i < _componentNames.Length; i++)
            {
                writer.WritePropertyName(_componentNames[i]);

                if (WritesIntegers)
                {
                    writer.WriteValue((long)components[i]);
                }
                else
                {
                    // doubleのまま書くと0.1fが0.10000000149011612のように長くなるため、floatに戻して書く。
                    writer.WriteValue((float)components[i]);
                }
            }

            writer.WriteEndObject();
        }


        /// <inheritdoc />
        public override T ReadJson(
            JsonReader reader,
            Type objectType,
            T existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.StartObject)
            {
                throw new JsonSerializationException(
                    $"{typeof(T).Name} にはオブジェクトが必要ですが、{reader.TokenType} が指定されています。");
            }

            Span<double> components = stackalloc double[_componentNames.Length];
            _defaultComponents.CopyTo(components);

            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                {
                    return Compose(components);
                }

                if (reader.TokenType != JsonToken.PropertyName)
                {
                    continue;
                }

                int index = Array.IndexOf(_componentNames, (string)reader.Value);

                if (!reader.Read())
                {
                    break;
                }

                if (index < 0)
                {
                    // 値がオブジェクトや配列でも、対応する終了トークンまで読み飛ばす。
                    reader.Skip();
                    continue;
                }

                components[index] = ReadNumber(reader);
            }

            throw new JsonSerializationException($"{typeof(T).Name} の読み込み中に、JSONが終わりました。");
        }


        /// <summary>
        /// 成分を四捨五入し、intの範囲に収めて返す。
        /// </summary>
        /// <param name="component">変換する成分。</param>
        /// <returns>四捨五入してintの範囲に収めた値を返す。</returns>
        /// <remarks>ファイルを手で編集して小数や範囲外の値が入っていても、読み込めるようにするため。</remarks>
        protected static int ToInt(double component)
        {
            // 引数を指定しないMath.Roundは、.5を偶数側に丸める（2.5は2になる）ため、0から遠い側に丸めて四捨五入にする。
            double rounded = Math.Round(component, MidpointRounding.AwayFromZero);
            return (int)Math.Clamp(rounded, int.MinValue, int.MaxValue);
        }


        /// <summary>
        /// 構造体を成分に分解する。
        /// </summary>
        /// <param name="value">分解する値。</param>
        /// <param name="components">成分の書き込み先。成分名と同じ順番で書き込む。</param>
        protected abstract void Decompose(T value, Span<double> components);


        /// <summary>
        /// 成分から構造体を組み立てる。
        /// </summary>
        /// <param name="components">成分名と同じ順番の成分。</param>
        /// <returns>組み立てた構造体を返す。</returns>
        protected abstract T Compose(ReadOnlySpan<double> components);


        private static double ReadNumber(JsonReader reader)
        {
            if (reader.TokenType != JsonToken.Integer && reader.TokenType != JsonToken.Float)
            {
                throw new JsonSerializationException(
                    $"{typeof(T).Name} の成分には数値が必要ですが、{reader.TokenType} が指定されています。");
            }

            return Convert.ToDouble(reader.Value, CultureInfo.InvariantCulture);
        }
    }
}
