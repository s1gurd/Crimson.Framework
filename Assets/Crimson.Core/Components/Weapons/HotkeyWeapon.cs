﻿using Crimson.Core.Common;
using Crimson.Core.Components;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Crimson.Core.Components.Weapons
{
	public class HotkeyWeapon : MonoBehaviour, IActorAbility
	{
		public InputActionReference _activateGravigunAction;
		public InputActionReference _changeAction;
		public InputActionReference _changeThrowableAction;
		public WeaponSlot _slot;
		public ThrowableSlot _throwableSlot;

		[ValidateInput(nameof(MustBeWeapon), "Perk MonoBehaviours must derive from IWeapon!")]
		public List<MonoBehaviour> Weapons;

		private IWeapon _gravityGun;

		public IActor Actor { get; set; }

		public List<IWeapon> EquipedWeapons { get; } = new List<IWeapon>();

		public List<IThrowable> EquipedThrowables { get; } = new List<IThrowable>();

		public void AddComponentData(ref Entity entity, IActor actor)
		{
			Actor = actor;
			EquipedWeapons.AddRange(Weapons.Cast<IWeapon>());
			if (_changeAction != null)
			{
				_changeAction.action.performed += ChangeActionHandler;
			}
			if (_changeThrowableAction != null)
			{
				_changeThrowableAction.action.performed += ChangeThrowableActionHandler;
			}
			if (_activateGravigunAction != null)
			{
				_activateGravigunAction.action.performed += ToggleGravigunHandler;
			}
			_slot.IsEnable = true;
		}

		public void Execute()
		{
		}

		internal void Add(IWeapon weapon)
		{
			if (weapon is GravityWeapon)
			{
				_gravityGun = weapon;
				_slot.Change(weapon);
			}
			else
			{
				EquipedWeapons.Add(weapon);
				_slot.Change(weapon);
			}
		}

		internal void Add(IThrowable throwable)
		{
			EquipedThrowables.Add(throwable);
			_throwableSlot.Change(throwable);
		}

		internal void UpdateUI()
		{
			_slot.UpdateUI();
		}

		private void ChangeActionHandler(InputAction.CallbackContext obj)
		{
			SelectNextWeapon();
		}

		private void ChangeThrowableActionHandler(InputAction.CallbackContext obj)
		{
			SelectNextThrowable();
		}

		private bool MustBeWeapon(List<MonoBehaviour> actions)
		{
			foreach (var action in actions)
			{
				if (action is IWeapon || action is null)
				{
					continue;
				}

				return false;
			}

			return true;
		}

		private void SelectNextWeapon()
		{
			var currentIndex = EquipedWeapons.IndexOf(_slot._weapon);
			var index = currentIndex == -1 ? 0 : (currentIndex + 1) % EquipedWeapons.Count;
			_slot.Change(EquipedWeapons[index]);
		}

		private void SelectNextThrowable()
		{
			var currentIndex = EquipedThrowables.IndexOf(_throwableSlot._weapon);
			var index = currentIndex == -1 ? 0 : (currentIndex + 1) % EquipedThrowables.Count;
			_throwableSlot.Change(EquipedThrowables[index]);
		}

		private void ToggleGravigunHandler(InputAction.CallbackContext obj)
		{
			if (_gravityGun == null)
			{
				return;
			}

			_slot.Change(_gravityGun);
		}

#if UNITY_EDITOR

		private void OnValidate()
		{
			if (_throwableSlot == null)
			{
				_throwableSlot = GetComponentInChildren<ThrowableSlot>();
			}
		}

#endif
	}
}