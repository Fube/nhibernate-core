using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace NHibernate.Util
{
	/// <summary>
	/// An <see cref="IDictionary" /> where keys are compared by object identity, rather than <c>equals</c>.
	/// 
	/// All external users of this class need to have no knowledge of the IdentityKey - it is all
	/// hidden by this class.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Do NOT use a System.Value type as the key for this Hashtable - only classes.  See
	/// the <a href="http://groups.google.com/groups?hl=en&amp;lr=&amp;ie=UTF-8&amp;oe=UTF-8&amp;threadm=bds2rm%24ruc%241%40charly.heeg.de&amp;rnum=1&amp;prev=/groups%3Fhl%3Den%26lr%3D%26ie%3DUTF-8%26oe%3DUTF-8%26q%3DSystem.Runtime.CompilerServices.RuntimeHelpers.GetHashCode%26sa%3DN%26tab%3Dwg">google thread</a>
	/// about why using System.Value is a bad thing.
	/// </para>
	/// <para>
	/// If I understand it correctly, the first call to get an object defined by a DateTime("2003-01-01")
	/// would box the DateTime and return the identity key for the box.  If you were to get that Key and
	/// unbox it into a DateTime struct, then the next time you passed it in as the Key the IdentityMap
	/// would box it again (into a different box) and it would have a different IdentityKey - so you would
	/// not get the same value for the same DateTime value. 
	/// </para>
	/// </remarks>
	[Serializable]
	public sealed class IdentityMap : IDictionary, IDeserializationCallback
	{
		// key = IdentityKey of the passed in Key
		// value = object passed in
		private IDictionary map;

		/// <summary>
		/// Create a new instance of the IdentityMap that has no 
		/// iteration order.
		/// </summary>
		/// <returns>A new IdentityMap based on a Hashtable.</returns>
		public static IDictionary Instantiate(int size)
		{
			return new IdentityMap(new Hashtable(size, ReferenceComparer<object>.Instance));
		}

		/// <summary>
		/// Create a new instance of the IdentityMap that has an 
		/// iteration order of the order the objects were added
		/// to the Map.
		/// </summary>
		/// <returns>A new IdentityMap based on ListDictionary.</returns>
		public static IDictionary InstantiateSequenced(int size)
		{
			return new IdentityMap(new SequencedHashMap(size, ReferenceComparer<object>.Instance));
		}

		/// <summary>
		/// Return the Dictionary Entries (as instances of <c>DictionaryEntry</c> in a collection
		/// that is safe from concurrent modification).  Ie - we may safely add new instances
		/// to the underlying <c>IDictionary</c> during enumeration of the <c>Values</c>.
		/// </summary>
		/// <param name="map">The IDictionary to get the enumeration safe list.</param>
		/// <returns>A Collection of DictionaryEntries</returns>
		// Since v5.8.
		[Obsolete("This method has no more usage in NHibernate and will be removed in a future version.")]		
		public static ICollection ConcurrentEntries(IDictionary map)
		{
			return ((IdentityMap)map).EntryList;
		}

		// Since v5.8.
		[Obsolete("This method has no more usage in NHibernate and will be removed in a future version.")]		
		public static ICollection Entries(IDictionary map)
		{
			return ((IdentityMap)map).EntryList;
		}

		/// <summary>
		/// Return the Dictionary Entries (as instances of <c>DictionaryEntry</c> in a list
		/// that is safe from concurrent modification).  Ie - we may safely add new instances
		/// to the underlying <c>IDictionary</c> during enumeration of the <c>Values</c>.
		/// </summary>
		/// <param name="map">The IDictionary to get the enumeration safe list.</param>
		/// <returns>A typed list of DictionaryEntries, avoiding boxing when built and enumerated.</returns>
		internal static List<DictionaryEntry> GetEntries(IDictionary map) => ((IdentityMap)map).GetEntries();

		/// <summary>
		/// Create the IdentityMap class with the correct class for the IDictionary.
		/// Unsorted = Hashtable
		/// Sorted = ListDictionary
		/// </summary>
		/// <param name="underlyingMap">A class that implements the IDictionary for storing the objects.</param>
		private IdentityMap(IDictionary underlyingMap)
		{
			this.map = underlyingMap;
		}

		/// <summary>
		/// <see cref="ICollection.Count"/>
		/// </summary>
		public int Count
		{
			get { return map.Count; }
		}

		/// <summary>
		/// <see cref="ICollection.IsSynchronized"/>
		/// </summary>
		public bool IsSynchronized
		{
			get { return map.IsSynchronized; }
		}

		/// <summary>
		/// <see cref="ICollection.SyncRoot"/>
		/// </summary>
		public object SyncRoot
		{
			get { return map.SyncRoot; }
		}

		/// <summary>
		/// <see cref="IDictionary.Add"/>
		/// </summary>
		public void Add(object key, object val)
		{
			map.Add(VerifyValidKey(key), val);
		}

		/// <summary>
		/// <see cref="IDictionary.Clear"/>
		/// </summary>
		public void Clear()
		{
			map.Clear();
		}

		/// <summary>
		/// <see cref="IDictionary.Contains"/>
		/// </summary>
		public bool Contains(object key)
		{
			if (key == null) return false;
			return map.Contains(VerifyValidKey(key));
		}

		/// <summary>
		/// <see cref="IEnumerable.GetEnumerator"/>
		/// </summary>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return map.GetEnumerator();
		}

		/// <summary>
		/// <see cref="IDictionary.GetEnumerator"/>
		/// </summary>
		public IDictionaryEnumerator GetEnumerator()
		{
			return map.GetEnumerator();
		}

		/// <summary>
		/// <see cref="IDictionary.IsFixedSize"/>
		/// </summary>
		public bool IsFixedSize
		{
			get { return map.IsFixedSize; }
		}

		/// <summary>
		/// <see cref="IDictionary.IsReadOnly"/>
		/// </summary>
		public bool IsReadOnly
		{
			get { return map.IsReadOnly; }
		}

		/// <summary>
		/// Returns the Keys used in this IdentityMap
		/// <see cref="IDictionary.IsReadOnly"/>
		/// </summary>
		public ICollection Keys
		{
			get { return map.Keys; }
		}

		/// <summary>
		/// <see cref="IDictionary.Remove"/>
		/// </summary>
		public void Remove(object key)
		{
			if (key == null) return;
			map.Remove(VerifyValidKey(key));
		}

		/// <summary>
		/// <see cref="IDictionary.this"/>
		/// </summary>
		public object this[object key]
		{
			get
			{
				if (key == null) return null;
				// Disable validation on get since it leads to better error messages
				//return map[ VerifyValidKey( key ) ];
				return map[key];
			}
			set { map[VerifyValidKey(key)] = value; }
		}

		/// <summary>
		/// <see cref="IDictionary.Values"/>
		/// </summary>
		public ICollection Values
		{
			get { return map.Values; }
		}

		/// <summary>
		/// <see cref="ICollection.CopyTo"/>
		/// </summary>
		/// <param name="array"></param>
		/// <param name="i"></param>
		public void CopyTo(Array array, int i)
		{
			map.CopyTo(array, i);
		}

		/// <summary>
		/// Provides a snapshot VIEW in the form of a List of the contents of the IdentityMap.
		/// You can safely iterate over this VIEW and modify the actual IdentityMap because the
		/// VIEW is a copy of the contents, not a reference to the existing Map.
		/// 
		/// Contains a copy (not that actual instance stored) of the DictionaryEntries in a List.
		/// </summary>
		// Since v5.8.
		[Obsolete("Use GetEntries instead.")]
		public IList EntryList => GetEntries();

		/// <summary>
		/// Provides a snapshot VIEW in the form of a typed List of the contents of the IdentityMap.
		/// You can safely iterate over this VIEW and modify the actual IdentityMap because the
		/// VIEW is a copy of the contents, not a reference to the existing Map.
		/// 
		/// Contains a copy (not the actual instance stored) of the DictionaryEntries in a List.
		/// </summary>
		/// <remarks>
		/// This uses <see cref="IDictionaryEnumerator"/> directly, which avoids boxing the
		/// <see cref="DictionaryEntry"/> structs when building the snapshot list.
		/// </remarks>
		public List<DictionaryEntry> GetEntries()
		{
			var list = new List<DictionaryEntry>(map.Count);
			if (map.Count == 0)
			{
				return list;
			}

			var enumerator = map.GetEnumerator();
			while (enumerator.MoveNext())
			{
				list.Add(enumerator.Entry);
			}

			return list;
		}

		/// <summary>
		/// Verifies that we are not using a System.ValueType as the Key in the Dictionary
		/// </summary>
		/// <param name="obj">The object that will be the key.</param>
		/// <returns>An object that is safe to be a key.</returns>
		/// <exception cref="ArgumentException">Thrown when the obj is a System.ValueType</exception>
		private object VerifyValidKey(object obj)
		{
			if (obj is ValueType)
			{
				throw new ArgumentException(
					"There is a problem with your mappings.  You are probably trying to map a System.ValueType to " +
					"a <class> which NHibernate does not allow or you are incorrectly using the " +
					"IDictionary that is mapped to a <set>.  \n\n" +
					"A ValueType (" + obj.GetType() + ") can not be used with IdentityKey.  " +
					"The thread at google has a good description about what happens with boxing " +
					"and unboxing ValueTypes and why they can not be used as an IdentityKey: " +
					"http://groups.google.com/groups?hl=en&lr=&ie=UTF-8&oe=UTF-8&threadm=bds2rm%24ruc%241%40charly.heeg.de&rnum=1&prev=/groups%3Fhl%3Den%26lr%3D%26ie%3DUTF-8%26oe%3DUTF-8%26q%3DSystem.Runtime.CompilerServices.RuntimeHelpers.GetHashCode%26sa%3DN%26tab%3Dwg"
					, "key");
			}

			return obj;
		}

		public static IDictionary Invert(IDictionary map)
		{
			IDictionary result = Instantiate(map.Count);
			foreach (DictionaryEntry me in map)
			{
				result[me.Value] = me.Key;
			}
			return result;
		}

		public void OnDeserialization(object sender)
		{
			((IDeserializationCallback)map).OnDeserialization(sender);
		}
	}

	internal static class IdentityMapUtils
	{
		/// <summary>
		/// Create a new instance of the IdentityMap that has no iteration order.
		/// </summary>
		/// <remarks>A new IdentityMap{ based on Dictionary, comparing keys by reference identity.</remarks>
		internal static IdentityMap<TKey, TValue> Instantiate<TKey, TValue>(int size) 
		where TKey: class
		where TValue: class
			=>
			new(new Dictionary<TKey, TValue>(size, ReferenceComparer<TKey>.Instance));
		
		/// <summary>
		/// Create a new instance of the IdentityMap that has an iteration order of the order the
		/// objects were added to the map, with a key moved back to the end whenever its value is
		/// overwritten through the indexer.
		/// </summary>
		/// <remarks>
		/// A new IdentityMap based on <see cref="SequencedSnapshotDictionary{TKey,TValue}"/>, comparing keys
		/// by reference identity.
		/// </remarks>
		internal static IdentityMap<TKey, TValue> InstantiateSequenced<TKey, TValue>(int size) 
			where TKey: class
			where TValue: class
			=>
				new(new SequencedSnapshotDictionary<TKey, TValue>(size, ReferenceComparer<TKey>.Instance));
		
		internal static IDictionary<TValue, TKey> Invert<TValue, TKey>(IDictionary<TKey, TValue> map)
			where TKey: class
			where TValue: class
		{
			IDictionary<TValue, TKey> result = Instantiate<TValue, TKey>(map.Count);
			foreach (var me in map)
			{
				result[me.Value] = me.Key;
			}
			return result;
		}
	}
	
	[Serializable]
	internal sealed class IdentityMap<TKey, TValue> : IDictionary<TKey, TValue>, IDictionary, IDeserializationCallback
		where TKey : class
		where TValue : class
	{
		// key = IdentityKey of the passed in Key
		// value = object passed in
		private readonly IDictionary<TKey, TValue> _genericMap;
		private readonly IDictionary _nonGenericMap;

		/// <summary>
		/// Create the IdentityMap class with the correct class for the underlying storage.
		/// </summary>
		/// <param name="underlyingMap">A class that implements the IDictionary for storing the objects.</param>
		internal IdentityMap(IDictionary<TKey, TValue> underlyingMap)
		{
			if (underlyingMap is not IDictionary nonGenericUnderlyingMap)
			{
				throw new ArgumentException(
					$"{nameof(underlyingMap)} must implement {nameof(IDictionary)}",
					nameof(underlyingMap));
			}
			
			_genericMap = underlyingMap;
			_nonGenericMap = nonGenericUnderlyingMap;
		}

		/// <returns>A typed, read-only snapshot of the DictionaryEntries, avoiding boxing when built and enumerated.</returns>
		internal static ISnapshotView<TKey, TValue> GetEntries(IDictionary<TKey, TValue> map) => 
			((IdentityMap<TKey, TValue>)map).GetEntries();

		internal ISnapshotView<TKey, TValue> GetEntries()
		{
			if (_genericMap is SequencedSnapshotDictionary<TKey, TValue> snapshotDictionary)
			{
				return snapshotDictionary.GetSnapshot();
			}

			return new SnapshotViewAdapter<TKey, TValue>(_genericMap.ToList());
		}

		/// <summary>
		/// Verifies that we are not using a System.ValueType as the Key in the Dictionary
		/// </summary>
		/// <param name="key">The object that will be the key.</param>
		/// <returns>An object that is safe to be a key.</returns>
		/// <exception cref="ArgumentException">Thrown when the obj is a System.ValueType</exception>
		private static Tk VerifyValidKey<Tk>(Tk key)
		{
			if (key is ValueType)
			{
				throw new ArgumentException(
					"There is a problem with your mappings.  You are probably trying to map a System.ValueType to " +
					"a <class> which NHibernate does not allow or you are incorrectly using the " +
					"IDictionary that is mapped to a <set>.  \n\n" +
					"A ValueType (" + key.GetType() + ") can not be used with IdentityKey.  " +
					"The thread at google has a good description about what happens with boxing " +
					"and unboxing ValueTypes and why they can not be used as an IdentityKey: " +
					"http://groups.google.com/groups?hl=en&lr=&ie=UTF-8&oe=UTF-8&threadm=bds2rm%24ruc%241%40charly.heeg.de&rnum=1&prev=/groups%3Fhl%3Den%26lr%3D%26ie%3DUTF-8%26oe%3DUTF-8%26q%3DSystem.Runtime.CompilerServices.RuntimeHelpers.GetHashCode%26sa%3DN%26tab%3Dwg"
					, nameof(key));
			}

			return key;
		}
		
		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _genericMap.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_genericMap).GetEnumerator();

		public void Add(KeyValuePair<TKey, TValue> item) => _genericMap.Add(VerifyValidKey(item.Key), item.Value);
		
		public void Clear() => _genericMap.Clear();
		
		public bool Contains(KeyValuePair<TKey, TValue> item)
		{
			VerifyValidKey(item.Key);
			return _genericMap.Contains(item);
		}

		public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => _genericMap.CopyTo(array, arrayIndex);

		public bool Remove(KeyValuePair<TKey, TValue> item)
		{
			VerifyValidKey(item.Key);
			return _genericMap.Remove(item);
		}
		
		public int Count => _genericMap.Count;
		
		public bool IsReadOnly => _genericMap.IsReadOnly;
		
		public bool ContainsKey(TKey key) => _genericMap.ContainsKey(VerifyValidKey(key));

		public void Add(TKey key, TValue value) => _genericMap.Add(VerifyValidKey(key), value);

		public bool Remove(TKey key) => _genericMap.Remove(VerifyValidKey(key));

		public bool TryGetValue(TKey key, out TValue value) => _genericMap.TryGetValue(VerifyValidKey(key), out value);

		public TValue this[TKey key]
		{
			get => _genericMap[VerifyValidKey(key)];
			set => _genericMap[VerifyValidKey(key)] = value;
		}

		public ICollection<TKey> Keys => _genericMap.Keys;
		
		public ICollection<TValue> Values => _genericMap.Values;
		
		// --- IDictionary explicit members ---
		
		IDictionaryEnumerator IDictionary.GetEnumerator() => _nonGenericMap.GetEnumerator();
		
		void IDictionary.Remove(object key) => _nonGenericMap.Remove(VerifyValidKey(key));

		object IDictionary.this[object key]
		{
			get => _nonGenericMap[VerifyValidKey(key)];
			set => _nonGenericMap[VerifyValidKey(key)] = value;
		}

		bool IDictionary.Contains(object key) => _nonGenericMap.Contains(key);

		void IDictionary.Add(object key, object value) => _nonGenericMap.Add(key, value);

		void ICollection.CopyTo(Array array, int index) => _nonGenericMap.CopyTo(array, index);
		
		object ICollection.SyncRoot => _nonGenericMap.SyncRoot;

		bool ICollection.IsSynchronized => _nonGenericMap.IsSynchronized;

		ICollection IDictionary.Values => _nonGenericMap.Values;

		bool IDictionary.IsFixedSize => _nonGenericMap.IsFixedSize;
		
		ICollection IDictionary.Keys => _nonGenericMap.Keys;
		
		void IDeserializationCallback.OnDeserialization(object sender) => ((IDeserializationCallback)_genericMap).OnDeserialization(sender);
	}
}
