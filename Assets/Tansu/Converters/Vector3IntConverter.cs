using System;
using UnityEngine;


namespace Tansu.Converters
{
    /// <summary>
    /// <see cref="Vector3Int"/>を<c>{"x":0,"y":0,"z":0}</c>の形式で読み書きする。
    /// </summary>
    public sealed class Vector3IntConverter : ComponentStructConverter<Vector3Int>
    {
        private static readonly string[] ComponentNames = { "x", "y", "z" };
        private static readonly double[] DefaultComponents = { 0, 0, 0 };


        protected override bool WritesIntegers => true;

        
        public Vector3IntConverter() : base(ComponentNames, DefaultComponents)
        {
        }


        protected override void Decompose(Vector3Int value, Span<double> components)
        {
            components[0] = value.x;
            components[1] = value.y;
            components[2] = value.z;
        }


        protected override Vector3Int Compose(ReadOnlySpan<double> components)
        {
            return new Vector3Int((int)components[0], (int)components[1], (int)components[2]);
        }
    }
}
