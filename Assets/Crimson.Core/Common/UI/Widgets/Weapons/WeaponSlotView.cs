﻿using Assets.Crimson.Core.Components.Weapons;
using Crimson.Core.Common;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Crimson.Core.Common.UI.Widgets.Weapons
{
	public class WeaponSlotView : MonoBehaviour, IUIElement
	{
		[ValueDropdown(nameof(UIAssociatedIds))]
		public string AssociatedValueID = "";

		[SerializeField] private CanvasGroup _canvas;
		[SerializeField] private bool _hideOnAwake;
		private UIReceiver _receiver;
		[SerializeField] private WeaponClipView _weaponClipView;
		[SerializeField] private WeaponNameLabel _weaponNameLabel;
		public string AssociatedID => AssociatedValueID;
		public bool IsVisible { get => _canvas.alpha == 1; set => _canvas.alpha = value ? 1 : 0; }

		public void SetData(object input)
		{
			if (!(input is IWeapon weapon))
			{
				IsVisible = false;
				return;
			}

			IsVisible = true;

			_weaponNameLabel.Set(weapon as IHasComponentName);
			_weaponClipView.Set(weapon as IHasClip);
		}

		private void Awake()
		{
			IsVisible = !_hideOnAwake;
		}

		private List<string> UIAssociatedIds()
		{
#if UNITY_EDITOR
			if (_receiver == null) _receiver = GetComponentInParent<UIReceiver>();
			return _receiver?.UIAssociatedIds;
#else
			return null;
#endif
		}
	}
}