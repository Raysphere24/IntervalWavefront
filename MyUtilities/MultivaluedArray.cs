#nullable enable

using System;
using System.Collections.Generic;

namespace MyUtilities;

public class MultivaluedArrayNode<T> where T : MultivaluedArrayNode<T>
{
	internal T? Next;
}

public class MultivaluedArray<T> where T : MultivaluedArrayNode<T>
{
	private readonly T?[] items;

	public int Length => items.Length;

	public MultivaluedArray(int size)
	{
		items = new T?[size];
	}

	public void Clear()
	{
		Array.Fill(items, null);
	}

	public IEnumerable<T> GetItems(int index)
	{
		T? item = items[index];

		while (item != null) {
			yield return item;
			item = item.Next;
		}
	}

	public void AddItem(int index, T item)
	{
		T? front = items[index];

		if (front != null) {
			item.Next = front;
		}

		items[index] = item;
	}
}
