using System;


namespace Tansu
{
    /// <summary>
    /// 保存する値の定義。キー名、型、既定値をまとめて持つ。
    /// </summary>
    /// <typeparam name="T">保存する値の型。</typeparam>
    /// <remarks>
    /// 値そのものは持たず、値を読み書きするときの鍵として使う。
    /// 型を鍵に含めることで、intで保存した値をstringとして読むような取り違えをコンパイル時に防ぐ。
    /// </remarks>
    public sealed class SaveValue<T>
    {
        /// <summary>値を識別する名前。</summary>
        /// <remarks>他の値と重複しない名前にする。「システム名.値の名前」（例: "player.coins"）の形にすると重複しにくい。</remarks>
        public string Key { get; }
        
        
        /// <summary>まだ一度も保存していないときに使う値。</summary>
        public T DefaultValue { get; }

        
        /// <param name="key">値を識別する名前。</param>
        /// <param name="defaultValue">まだ一度も保存していないときに使う値。</param>
        /// <exception cref="ArgumentException"><paramref name="key"/>が空、または空白だけの場合。</exception>
        public SaveValue(string key, T defaultValue)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key must not be empty.", nameof(key));
            }

            Key = key;
            DefaultValue = defaultValue;
        }


        /// <summary>
        /// 型とキー名を含む文字列を返す。ログやデバッグ表示に使う。
        /// </summary>
        public override string ToString()
        {
            return $"SaveValue<{typeof(T).Name}>({Key})";
        }
    }
}
