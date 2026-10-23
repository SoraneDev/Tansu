using System;
using UnityEngine;


namespace Tansu.Converters
{
    /// <summary>
    /// <see cref="Quaternion"/>を<c>{"x":0,"y":0,"z":0,"w":1}</c>の形式で読み書きする。
    /// </summary>
    /// <remarks>
    /// 成分が欠けている場合に、回転として不正な値（すべて0）ではなく無回転になるよう、既定値を単位クォータニオンにしている。
    /// </remarks>
    public sealed class QuaternionConverter : ComponentStructConverter<Quaternion>
    {
        private static readonly string[] ComponentNames = { "x", "y", "z", "w" };
        private static readonly double[] DefaultComponents = { 0, 0, 0, 1 };

        
        public QuaternionConverter() : base(ComponentNames, DefaultComponents)
        {
        }


        protected override void Decompose(Quaternion value, Span<double> components)
        {
            components[0] = value.x;
            components[1] = value.y;
            components[2] = value.z;
            components[3] = value.w;
        }


        protected override Quaternion Compose(ReadOnlySpan<double> components)
        {
            return new Quaternion((float)components[0], (float)components[1], (float)components[2], (float)components[3]);
        }
    }
}
