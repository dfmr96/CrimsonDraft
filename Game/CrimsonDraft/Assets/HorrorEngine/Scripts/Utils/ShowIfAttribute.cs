using UnityEngine;

namespace HorrorEngine
{
	public enum MultiOp
	{
		None = -1,
		/// <summary>Checks if the field's value equals one of the requirements.</summary>
		Equals,
		/// <summary>Checks if the field's value differs from all the requirements.</summary>
		Diff,
	}

	public enum Op
	{
		None = -1,
		/// <summary>Checks if the field's value is equal to the requirement.</summary>
		Equals,
		/// <summary>Checks if the field's value is different from the requirement.</summary>
		Diff,
		/// <summary>Checks if the field's value is superior than the requirement.</summary>
		Sup,
		/// <summary>Checks if the field's value is less than the requirement.</summary>
		Inf,
		/// <summary>Checks if the field's value is greater or equal to the requirement.</summary>
		SupEquals,
		/// <summary>Checks if the field's value is less than or equal to the requirement.</summary>
		InfEquals,
		/// <summary>Checks if the field's bitmask contains the raised bit.</summary>
		BitmaskContain,
		/// <summary>Checks if the field's bitmask contains the raised bit.</summary>
		BitmaskNotContain,
		/// <summary>Checks if the field's bitmask contains the raised bit.</summary>
		LocalizedRefSet,
		/// <summary>Checks if the field's bitmask contains the raised bit.</summary>
		LocalizedRefNotSet,
	}

	public class ShowIfAttribute : PropertyAttribute
	{
		public readonly string fieldName;
		public readonly Op @operator;
		public readonly MultiOp multiOperator;
		public readonly object[] values;

		public ShowIfAttribute(string fieldName, Op @operator = Op.Equals, object value = null)
		{
			this.fieldName = fieldName;
			this.@operator = @operator;
			this.multiOperator = MultiOp.None;
			this.values = new object[] { value ?? true };
		}

		public ShowIfAttribute(string fieldName, MultiOp multiOperator, params object[] values)
		{
			this.fieldName = fieldName;
			this.@operator = Op.None;
			this.multiOperator = multiOperator;
			this.values = values;
		}
	}

	public class HideIfAttribute : PropertyAttribute
	{
		public readonly string fieldName;
		public readonly Op @operator;
		public readonly MultiOp multiOperator;
		public readonly object[] values;

		public HideIfAttribute(string fieldName, Op @operator = Op.Equals, object value = null)
		{
			this.fieldName = fieldName;
			this.@operator = @operator;
			this.multiOperator = MultiOp.None;
			this.values = new object[] { value ?? true};
		}

		public HideIfAttribute(string fieldName, MultiOp multiOperator, params object[] values)
		{
			this.fieldName = fieldName;
			this.@operator = Op.None;
			this.multiOperator = multiOperator;
			this.values = values;
		}
	}
}