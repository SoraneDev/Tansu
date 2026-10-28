namespace Tansu
{
    /// <summary>
    /// 保存する値の読み書きを提供する。
    /// </summary>
    public interface ISaveValues
    {
        /// <summary>
        /// 値を読み込む。まだ一度も保存していない場合は、既定値を返す。
        /// </summary>
        /// <typeparam name="T">値の型。</typeparam>
        /// <param name="saveValue">読み込む値の定義。</param>
        /// <returns>保存されている値。保存されていない場合は既定値を返す。</returns>
        /// <remarks>
        /// 毎回新しく作った値を返す。<see cref="System.Collections.Generic.List{T}" />などの中身を変えても、保存している値は変わらない。
        /// 変更を反映するには<see cref="Set{T}" />を呼ぶ。
        /// 保存されている値を<typeparamref name="T" />として読めない場合は、警告を出して既定値を返す。
        /// </remarks>
        T Get<T>(SaveValue<T> saveValue);


        /// <summary>
        /// 値を書き換える。ファイルには書き込まない。
        /// </summary>
        /// <typeparam name="T">値の型。</typeparam>
        /// <param name="saveValue">書き換える値の定義。</param>
        /// <param name="value">設定する値。</param>
        /// <remarks>今と同じ値を設定した場合は、変更として扱わない。</remarks>
        void Set<T>(SaveValue<T> saveValue, T value);


        /// <summary>
        /// 値が設定されているかどうかを取得する。まだファイルに書き込んでいない値も含む。
        /// </summary>
        /// <typeparam name="T">値の型。</typeparam>
        /// <param name="saveValue">調べる値の定義。</param>
        /// <returns>
        /// 値が設定されている場合はtrue、
        /// <see cref="Get{T}" />が既定値を返す状態の場合はfalseを返す。
        /// </returns>
        bool Has<T>(SaveValue<T> saveValue);


        /// <summary>
        /// 値を削除する。以降の<see cref="Get{T}" />は既定値を返す。ファイルには書き込まない。
        /// </summary>
        /// <typeparam name="T">値の型。</typeparam>
        /// <param name="saveValue">削除する値の定義。</param>
        void Remove<T>(SaveValue<T> saveValue);
    }
}
