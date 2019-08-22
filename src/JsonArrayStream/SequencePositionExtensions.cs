using System;

namespace JsonArrayStream
{
	public static class SequencePositionExtensions
	{
		public static SequencePosition Add(this SequencePosition x, int y)
			=> new SequencePosition(x.GetObject(), x.GetInteger() + y);
		public static SequencePosition? Min(SequencePosition? x, SequencePosition? y) =>
			x is null && y is null ? null :
			x is null ? y :
			y is null ? x :
			Min(x.Value, y.Value);
		public static SequencePosition Min(SequencePosition x, SequencePosition y) =>
			x.GetInteger() < y.GetInteger() ? x : y;
	}
}
