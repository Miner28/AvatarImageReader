using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Experimental.UIElements;
using UnityEngine.Experimental.UIElements;
using System;
using System.Linq;
using System.Reflection;
using System.Globalization;
using System.Reflection.Emit;
using VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI
{
	public static class UdonFieldFactory
	{
		static readonly Dictionary< Type, Type >    fieldDrawers = new Dictionary< Type, Type >();

		static readonly MethodInfo	        		createFieldMethod = typeof(UdonFieldFactory).GetMethod("CreateFieldSpecific", BindingFlags.Static | BindingFlags.Public);

		static UdonFieldFactory()
		{
			AddDrawer(typeof(bool), typeof(Toggle));
            AddDrawer(typeof(int), typeof(IntegerField));
            AddDrawer(typeof(long), typeof(LongField));
            AddDrawer(typeof(float), typeof(FloatField));
			AddDrawer(typeof(double), typeof(DoubleField));
			AddDrawer(typeof(string), typeof(TextField));
			AddDrawer(typeof(Bounds), typeof(BoundsField));
			AddDrawer(typeof(Color), typeof(ColorField));
			AddDrawer(typeof(Vector2), typeof(Vector2Field));
			AddDrawer(typeof(Vector2Int), typeof(Vector2IntField));
			AddDrawer(typeof(Vector3), typeof(Vector3Field));
			AddDrawer(typeof(Vector3Int), typeof(Vector3IntField));
			AddDrawer(typeof(Vector4), typeof(Vector4Field));
			AddDrawer(typeof(Enum), typeof(EnumField));
			AddDrawer(typeof(UnityEngine.Object), typeof(ObjectField));
			AddDrawer(typeof(Rect), typeof(RectField));
			AddDrawer(typeof(RectInt), typeof(RectIntField));
			AddDrawer(typeof(char), typeof(CharField));
			AddDrawer(typeof(byte), typeof(ByteField));
			AddDrawer(typeof(Quaternion), typeof(QuaternionField));
			AddDrawer(typeof(LayerMask), typeof(LayerMaskField));
			AddDrawer(typeof(SDKBase.VRCUrl), typeof(VRCUrlField));
		}

		static void AddDrawer(Type fieldType, Type drawerType)
		{
			var iNotifyType = typeof(INotifyValueChanged<>).MakeGenericType(fieldType);

			if (!iNotifyType.IsAssignableFrom(drawerType))
			{
				Debug.LogWarning("The custom field drawer " + drawerType + " does not implements INotifyValueChanged< " + fieldType + " >");
				return ;
			}

			fieldDrawers[fieldType] = drawerType;
		}

		public static INotifyValueChanged< T > CreateField< T >(T value)
		{
			return CreateField(value != null ? value.GetType() : typeof(T)) as INotifyValueChanged< T >;
		}

		public static VisualElement CreateField(Type t)
		{
			Type drawerType;

			fieldDrawers.TryGetValue(t, out drawerType);

			if (drawerType == null)
				drawerType = fieldDrawers.FirstOrDefault(kp => kp.Key.IsReallyAssignableFrom(t)).Value;

			if (drawerType == null)
			{
				return null;
			}
			
			object field;
			
			if (drawerType == typeof(EnumField))
			{
				field = new EnumField(Activator.CreateInstance(t) as Enum);
			}
			else
			{
				try {
					field = Activator.CreateInstance(drawerType,
						BindingFlags.CreateInstance |
						BindingFlags.Public |
						BindingFlags.NonPublic |
						BindingFlags.Instance | 
						BindingFlags.OptionalParamBinding, null,
						new object[]{Type.Missing }, CultureInfo.CurrentCulture);
				} catch {
					field = Activator.CreateInstance(drawerType,
						BindingFlags.CreateInstance |
						BindingFlags.Public |
						BindingFlags.NonPublic |
						BindingFlags.Instance | 
						BindingFlags.OptionalParamBinding, null,
						new object[]{}, CultureInfo.CurrentCulture);
				}
			}

			// For mutiline
			if (field is TextField textField)
				textField.multiline = true;
			
			return field as VisualElement;
		}

		public static INotifyValueChanged< T > CreateFieldSpecific< T >(T value, Action< object > onValueChanged)
		{
			var fieldDrawer = CreateField< T >(value);

			if (fieldDrawer == null)
				return null;

			fieldDrawer.value = value;
			if (onValueChanged != null)
			{
				fieldDrawer.OnValueChanged((e) => onValueChanged(e.newValue));	
			}
			return fieldDrawer as INotifyValueChanged< T >;
		}

		public static VisualElement CreateField(Type fieldType, object value, Action< object > onValueChanged)
		{
			if (typeof(Enum).IsAssignableFrom(fieldType))
				fieldType = typeof(Enum);
			
			if (fieldType.IsArray)
			{
				return new UdonArrayEditor(fieldType, onValueChanged, value);
			}

			VisualElement field = null;

			try
			{
				var createFieldSpecificMethod = createFieldMethod.MakeGenericMethod(fieldType);
				field = createFieldSpecificMethod.Invoke(null, new object[]{value, onValueChanged}) as VisualElement;

				// delay textFields
				if (field is TextField) ((TextField) field).isDelayed = true;
			}
			catch (Exception e)
			{
				Debug.LogError(e);
			}

			return field;
		}
	}
}
