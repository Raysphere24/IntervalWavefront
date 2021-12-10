#nullable enable

using System;
using System.Collections.Immutable;

namespace MyUtilities;

public class TypedIndexedArray<TKey, TValue>
	where TKey : IndexedElement
{
	private readonly TValue[] items;

	public TypedIndexedArray(int size)
	{
		items = new TValue[size];
	}

	public void Fill(TValue value) => Array.Fill(items, value);

	public TValue this[TKey key]
	{
		get => items[key.Index];
		set => items[key.Index] = value;
	}

	public ImmutableArray<TValue> ToImmutableArray() => ImmutableArray.Create(items);
}
