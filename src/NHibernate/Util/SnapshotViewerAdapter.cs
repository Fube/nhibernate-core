using System.Collections;
using System.Collections.Generic;

namespace NHibernate.Util;

internal sealed class SnapshotViewAdapter<TKey, TValue> : ISnapshotView<TKey, TValue>
{
	private readonly IReadOnlyCollection<KeyValuePair<TKey, TValue>> _keyValuePairs;

	internal SnapshotViewAdapter(IReadOnlyCollection<KeyValuePair<TKey, TValue>> keyValuePairs) => _keyValuePairs = keyValuePairs;

	public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _keyValuePairs.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public int Count => _keyValuePairs.Count;
	public void Dispose() {}
}
