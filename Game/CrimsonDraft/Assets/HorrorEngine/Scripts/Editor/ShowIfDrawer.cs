using System;
using System.Reflection;
using UnityEditor;

namespace HorrorEngine
{
	using UnityEngine;
    using UnityEngine.Localization;

    [CustomPropertyDrawer(typeof(ShowIfAttribute))]
	internal sealed class ShowIfDrawer : PropertyDrawer
	{
		private ConditionalRenderer renderer;

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			if (this.renderer == null)
				this.renderer = new ConditionalRenderer("ShowIf", this, base.GetPropertyHeight, true);

			return this.renderer.GetPropertyHeight(property, label);
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			this.renderer.OnGUI(position, property, label);
		}
	}

	[CustomPropertyDrawer(typeof(HideIfAttribute))]
	internal sealed class HideIfDrawer : PropertyDrawer
	{
		private ConditionalRenderer renderer;

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			if (this.renderer == null)
				this.renderer = new ConditionalRenderer("HideIf", this, base.GetPropertyHeight, false);

			return this.renderer.GetPropertyHeight(property, label);
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			this.renderer.OnGUI(position, property, label);
		}
	}

	internal sealed class ConditionalRenderer
	{
		private const float EmptyHeight = -2F;

		private string name;
		private Func<SerializedProperty, GUIContent, float> getPropertyHeight;
		private PropertyDrawer drawer;
		private bool normalBooleanValue;

		private string errorAttribute = null;
		private FieldInfo conditionField;
		private string fieldName;
		private Op @operator;
		private MultiOp multiOperator;
		private object[] values;

		private object lastValue;
		private string lastValueStringified;
		private string[] targetValueStringified;
		private Decimal[] targetValueDecimaled;

		private bool conditionResult;
		private bool invalidHeight = true;
		private float cachedHeight;

		private Func<SerializedProperty, GUIContent, float> PropertyHeight;

		public ConditionalRenderer(string name, PropertyDrawer drawer, Func<SerializedProperty, GUIContent, float> getPropertyHeight, bool normalBooleanValue)
		{
			this.name = name;
			this.drawer = drawer;
			this.getPropertyHeight = getPropertyHeight;
			this.normalBooleanValue = normalBooleanValue;
		}

		public float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			if (this.fieldName == null)
				this.InitializeDrawer(property);

			if (this.errorAttribute != null)
				return 16F;
			if (this.conditionField == null)
				return this.getPropertyHeight(property, label);

			return this.PropertyHeight(property, label);
		}

		public void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			if (this.errorAttribute != null)
			{
				Color restore = GUI.contentColor;
				GUI.contentColor = Color.black;
				EditorGUI.LabelField(position, label.text, this.errorAttribute);
				GUI.contentColor = restore;
			}
			else if (this.conditionField == null || this.conditionResult == this.normalBooleanValue)
			{
				EditorGUI.BeginChangeCheck();
				EditorGUI.PropertyField(position, property, label, property.isExpanded);
				if (EditorGUI.EndChangeCheck() == true)
					this.invalidHeight = true;
			}
		}

		private void InitializeDrawer(SerializedProperty property)
		{
			ShowIfAttribute showIfAttr = (this.drawer.attribute as ShowIfAttribute);

			if (showIfAttr != null)
			{
				this.fieldName = showIfAttr.fieldName;
				this.@operator = showIfAttr.@operator;
				this.multiOperator = showIfAttr.multiOperator;
				this.values = showIfAttr.values;
			}
			else
			{
				HideIfAttribute hideIfAttr = (this.drawer.attribute as HideIfAttribute);

				if (hideIfAttr != null)
				{
					this.fieldName = hideIfAttr.fieldName;
					this.@operator = hideIfAttr.@operator;
					this.multiOperator = hideIfAttr.multiOperator;
					this.values = hideIfAttr.values;
				}
				else
					this.errorAttribute = "ShowIfAttribute or HideIfAttribute is required by field " + this.name + ".";
			}

			this.conditionField = this.drawer.fieldInfo.DeclaringType.GetField(this.fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			if (this.conditionField == null)
			{
				this.errorAttribute = this.name + " is requiring field \"" + this.fieldName + "\".";
				return;
			}
			else if (this.@operator != Op.None)
			{
				if (this.values[0] == null)
				{
					this.targetValueStringified = new string[] { string.Empty };
					this.PropertyHeight = this.GetHeightAllOpsString;

					if (this.@operator != Op.Equals &&
						this.@operator != Op.Diff)
					{
						this.errorAttribute = this.name + " is requiring a null value whereas its operator is \"" + this.@operator + "\" which is impossible.";
					}
				}
				else if (this.values[0] is Boolean)
				{
					this.targetValueStringified = new string[] { this.values[0].ToString() };
					this.PropertyHeight = this.GetHeightAllOpsString;

					if (this.@operator != Op.Equals &&
						this.@operator != Op.Diff &&
						this.@operator != Op.LocalizedRefSet &&
						this.@operator != Op.LocalizedRefNotSet)
					{
						this.errorAttribute = this.name + " is requiring a boolean whereas its operator is \"" + this.@operator + "\" which is impossible.";
					}
				}
				else if (this.values[0] is Int32 ||
						 this.values[0] is Single ||
						 this.values[0] is Enum ||
						 this.values[0] is Double ||
						 this.values[0] is Decimal ||
						 this.values[0] is Int16 ||
						 this.values[0] is Int64 ||
						 this.values[0] is UInt16 ||
						 this.values[0] is UInt32 ||
						 this.values[0] is UInt64 ||
						 this.values[0] is Byte ||
						 this.values[0] is SByte)
				{
					this.targetValueDecimaled = new Decimal[] { Convert.ToDecimal(this.values[0]) };
					this.PropertyHeight = this.GetHeightAllOpsScalar;
				}
				else
				{
					this.targetValueStringified = new string[] { this.values[0].ToString() };
					this.PropertyHeight = this.GetHeightAllOpsString;
				}
			}
			else if (this.multiOperator != MultiOp.None)
			{
				if (this.CheckUseOfNonScalarValue() == true)
				{
					this.targetValueStringified = new string[this.values.Length];
					for (int i = 0; i < this.values.Length; i++)
					{
						if (this.values[i] != null)
							this.targetValueStringified[i] = this.values[i].ToString();
						else
							this.targetValueStringified[i] = string.Empty;
					}

					this.PropertyHeight = this.GetHeightMultiOpsString;
				}
				else
				{
					this.targetValueDecimaled = new Decimal[this.values.Length];
					for (int i = 0; i < this.values.Length; i++)
						this.targetValueDecimaled[i] = Convert.ToDecimal(this.values[i]);

					this.PropertyHeight = this.GetHeightMultiOpsScalar;
				}
			}

			// Force the next update.
			if (property.serializedObject.targetObject.GetType().GetField(this.conditionField.Name) != null)
			{
				object newValue = this.conditionField.GetValue(property.serializedObject.targetObject);

				if (this.lastValue == newValue)
					this.lastValue = true;
			}

		}

		private bool CheckUseOfNonScalarValue()
		{
			for (int i = 0; i < this.values.Length; i++)
			{
				if (this.values[i] == null ||
					this.values[i] is String ||
					this.values[i] is Boolean)
				{
					return true;
				}
			}

			return false;
		}

		private float GetHeightAllOpsString(SerializedProperty property, GUIContent label)
		{
			if (property.serializedObject.targetObject.GetType().GetField(this.conditionField.Name) == null) 
				return 0f;

			object newValue = this.conditionField.GetValue(property.serializedObject.targetObject);

			if (this.lastValue != newValue)
			{
				this.lastValue = newValue;

				if (this.lastValue != null &&
					// Unity Object is not referenced as real null, it is fake. Don't trust them.
					(typeof(Object).IsAssignableFrom(this.lastValue.GetType()) == false ||
					 ((this.lastValue as Object).ToString() != "null")))
				{
					this.lastValueStringified = this.lastValue.ToString();
				}
				else
					this.lastValueStringified = string.Empty;

				if (this.@operator == Op.Equals)
					this.conditionResult = this.lastValueStringified.Equals(this.targetValueStringified[0]);
				else if (this.@operator == Op.Diff)
					this.conditionResult = this.lastValueStringified.Equals(this.targetValueStringified[0]) == false;
				else if (this.@operator == Op.Sup)
					this.conditionResult = this.lastValueStringified.CompareTo(this.targetValueStringified[0]) > 0;
				else if (this.@operator == Op.Inf)
					this.conditionResult = this.lastValueStringified.CompareTo(this.targetValueStringified[0]) < 0;
				else if (this.@operator == Op.SupEquals)
					this.conditionResult = this.lastValueStringified.CompareTo(this.targetValueStringified[0]) >= 0;
				else if (this.@operator == Op.InfEquals)
					this.conditionResult = this.lastValueStringified.CompareTo(this.targetValueStringified[0]) <= 0;
				else if (this.@operator == Op.LocalizedRefNotSet)
				{
					LocalizedReference localizedStr = (LocalizedReference)newValue;
					this.conditionResult = localizedStr != null && localizedStr.IsEmpty;
				}
				else if (this.@operator == Op.LocalizedRefSet)
				{
					LocalizedReference localizedStr = (LocalizedReference)newValue;
					this.conditionResult = localizedStr != null && !localizedStr.IsEmpty;
				}
			}

			return this.CalculateHeight(property, label);
		}

		private float GetHeightAllOpsScalar(SerializedProperty property, GUIContent label)
		{
			object newValue = this.conditionField.GetValue(property.serializedObject.targetObject);

			if (newValue.Equals(this.lastValue) == false)
			{
				this.lastValue = newValue;

				try
				{
					Decimal value = Convert.ToDecimal(newValue);

					if (this.@operator == Op.Equals)
						this.conditionResult = value == this.targetValueDecimaled[0];
					else if (this.@operator == Op.Diff)
						this.conditionResult = value != this.targetValueDecimaled[0];
					else if (this.@operator == Op.Sup)
						this.conditionResult = value > this.targetValueDecimaled[0];
					else if (this.@operator == Op.Inf)
						this.conditionResult = value < this.targetValueDecimaled[0];
					else if (this.@operator == Op.SupEquals)
						this.conditionResult = value >= this.targetValueDecimaled[0];
					else if (this.@operator == Op.InfEquals)
						this.conditionResult = value <= this.targetValueDecimaled[0];
					else if (this.@operator == Op.BitmaskContain)
						this.conditionResult = ((int)value & (int)this.targetValueDecimaled[0]) != 0;
					else if (this.@operator == Op.BitmaskNotContain)
						this.conditionResult = ((int)value & (int)this.targetValueDecimaled[0]) == 0;
					
				}
				catch
				{
				}
			}

			return this.CalculateHeight(property, label);
		}

		private float GetHeightMultiOpsString(SerializedProperty property, GUIContent label)
		{
			object newValue = this.conditionField.GetValue(property.serializedObject.targetObject);

			if (this.lastValue != newValue)
			{
				this.lastValue = newValue;

				if (this.lastValue != null &&
					// Unity Object is not referenced as real null, it is fake. Don't trust them.
					(typeof(Object).IsAssignableFrom(this.lastValue.GetType()) == false ||
					 ((this.lastValue as Object).ToString() != "null")))
				{
					this.lastValueStringified = this.lastValue.ToString();
				}
				else
					this.lastValueStringified = string.Empty;

				if (this.multiOperator == MultiOp.Equals)
				{
					this.conditionResult = !this.normalBooleanValue;

					for (int i = 0; i < this.targetValueStringified.Length; i++)
					{
						if (this.lastValueStringified.Equals(this.targetValueStringified[i]) == true)
						{
							this.conditionResult = this.normalBooleanValue;
							break;
						}
					}
				}
				else if (this.multiOperator == MultiOp.Diff)
				{
					int i = 0;

					this.conditionResult = this.normalBooleanValue;

					for (; i < this.targetValueStringified.Length; i++)
					{
						if (this.lastValueStringified.Equals(this.targetValueStringified[i]) == true)
						{
							this.conditionResult = !this.normalBooleanValue;
							break;
						}
					}
				}
			}

			return this.CalculateHeight(property, label);
		}

		private float GetHeightMultiOpsScalar(SerializedProperty property, GUIContent label)
		{
			object newValue = this.conditionField.GetValue(property.serializedObject.targetObject);

			if (newValue.Equals(this.lastValue) == false)
			{
				this.lastValue = newValue;

				try
				{
					Decimal value = Convert.ToDecimal(newValue);

					if (this.multiOperator == MultiOp.Equals)
					{
						this.conditionResult = !this.normalBooleanValue;

						for (int i = 0; i < this.targetValueDecimaled.Length; i++)
						{
							if (value == this.targetValueDecimaled[i])
							{
								this.conditionResult = this.normalBooleanValue;
								break;
							}
						}
					}
					else if (this.multiOperator == MultiOp.Diff)
					{
						int i = 0;

						this.conditionResult = this.normalBooleanValue;

						for (; i < this.targetValueDecimaled.Length; i++)
						{
							if (value == this.targetValueDecimaled[i])
							{
								this.conditionResult = !this.normalBooleanValue;
								break;
							}
						}
					}
				}
				catch
				{
				}
			}

			return this.CalculateHeight(property, label);
		}

		private float CalculateHeight(SerializedProperty property, GUIContent label)
		{
			if (this.conditionResult == this.normalBooleanValue)
			{
				if (this.invalidHeight == true)
				{
					this.invalidHeight = false;
					this.cachedHeight = EditorGUI.GetPropertyHeight(property, label, property.isExpanded);
				}

				return this.cachedHeight;
			}

			return ConditionalRenderer.EmptyHeight;
		}
	}
}