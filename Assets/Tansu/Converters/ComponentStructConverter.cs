using System;
using System.Globalization;
using Newtonsoft.Json;


namespace Tansu.Converters
{
    /// <summary>
    /// 数値の成分だけで構成される構造体（Vector3、Colorなど）を、<c>{"x":1,"y":2}</c>の形式で読み書きする。
    /// </summary>
    /// <typeparam name="T">対象の構造体。</typeparam>
    /// <remarks>
    /// 読み込み時、JSONに存在しない成分は生成時に指定した既定値になり、未知のプロパティは読み飛ばす。
    /// 成分は内部でdoubleとして扱う。floatとintのどちらも誤差なく表現できるため。
    /// </remarks>
    public abstract class ComponentStructConverter<T> : JsonConverter<T> where T : struct
    {
        private readonly string[] _componentNames;
        private readonly double[] _defaultComponents;


        /// <summary>
        /// 成分を整数として書き出すかどうか。
        /// </summary>
        protected virtual bool WritesIntegers => false;


        /// <summary>
        /// 成分名とJSONに存在しない成分の既定値を指定して作る。
        /// </summary>
        /// <param name="componentNames">JSON上の成分名。並び順が成分の順番になる。</param>
        /// <param name="defaultComponents">JSONに存在しない成分の既定値。<paramref name="componentNames"/>と同じ長さ。</param>
        /// <exception cref="ArgumentException">2つの配列の長さが異なる場合。</exception>
        protected ComponentStructConverter(string[] componentNames, double[] defaultComponents)
        {
            if (componentNames.Length != defaultComponents.Length)
            {
                throw new ArgumentException("Component names and default values must have the same length.");
            }

            _componentNames = componentNames;
            _defaultComponents = defaultComponents;
        }

        
        public override void WriteJson(JsonWriter writer, T value, JsonSerializer serializer)
        {
            Span<double> components = stackalloc double[_componentNames.Length];
            Decompose(value, components);

            writer.WriteStartObject();
            for (var i = 0; i < _componentNames.Length; i++)
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
        
    
        public override T ReadJson(JsonReader reader, Type objectType, T existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.StartObject)
            {
                throw new JsonSerializationException($"Expected an object for {typeof(T).Name}, but got {reader.TokenType}.");
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

                var index = Array.IndexOf(_componentNames, (string)reader.Value);
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

            throw new JsonSerializationException($"Unexpected end of JSON while reading {typeof(T).Name}.");
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
        protected abstract T Compose(ReadOnlySpan<double> components);


        private static double ReadNumber(JsonReader reader)
        {
            if (reader.TokenType != JsonToken.Integer && reader.TokenType != JsonToken.Float)
            {
                throw new JsonSerializationException($"Expected a number for a component of {typeof(T).Name}, but got {reader.TokenType}.");
            }

            return Convert.ToDouble(reader.Value, CultureInfo.InvariantCulture);
        }
    }
}
