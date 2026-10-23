namespace Tansu
{
    /// <summary>
    /// 保存する値を読み書きするためのインターフェース。
    /// </summary>
    public interface ISaveValues
    {
        /// <summary>
        /// 値を読み込む。まだ一度も保存していなければ、既定値を返す。
        /// </summary>
        /// <remarks>
        /// 返すのは毎回新しく作った値である。Listなどを受け取って中身を変えても、ストアの値は変わらない。
        /// 変更を反映するには<see cref="Set{T}"/>を呼ぶ。
        /// 保存されている値を<typeparamref name="T"/>として読めない場合は、警告を出して既定値を返す。
        /// </remarks>
        /// <returns>保存されている値。なければ既定値。</returns>
        T Get<T>(SaveValue<T> saveValue);
        
        
        /// <summary>
        /// 値を書き換える。ファイルには書き込まない。
        /// </summary>
        /// <remarks>
        /// 今と同じ値を設定した場合は、変更として扱わない。
        /// </remarks>
        void Set<T>(SaveValue<T> saveValue, T value);
        
        
        /// <summary>
        /// 値が設定されているかどうかを返す。まだファイルに書き込んでいない値も含む。
        /// </summary>
        /// <returns>値が設定されていればtrue。<see cref="Get{T}"/>が既定値を返す状態ならfalse。</returns>
        bool Has<T>(SaveValue<T> saveValue);
        
        
        /// <summary>
        /// 値を削除する。以降の<see cref="Get{T}"/>は既定値を返す。ファイルには書き込まない。
        /// </summary>
        void Remove<T>(SaveValue<T> saveValue);
    }
}
