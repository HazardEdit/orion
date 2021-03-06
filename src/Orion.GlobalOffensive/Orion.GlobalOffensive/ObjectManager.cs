﻿// Copyright (C) 2015 aevitas
// See the file LICENSE for copying permission.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using log4net;
using Orion.Common;
using Orion.GlobalOffensive.Objects;
using Orion.GlobalOffensive.Patchables;

namespace Orion.GlobalOffensive
{
	/// <summary>
	///     Manages entities within the game world.
	/// </summary>
	public class ObjectManager : NativeObject
	{
		private readonly ILog _log = Log.Get();
		// Obtain this dynamically from the game at a later stage.
		private readonly int _capacity;
		// Exposed through a read-only list, users of the API won't be able to change what's going on in game anyway.
		private readonly List<BaseEntity> _players = new List<BaseEntity>();
		private readonly int _ticksPerSecond;
		private DateTime _lastUpdate = DateTime.MinValue;

		/// <summary>
		///     Initializes a new instance of the <see cref="ObjectManager" /> class.
		/// </summary>
		/// <param name="baseAddress">The base address.</param>
		/// <param name="capacity">The capacity.</param>
		/// <param name="ticksPerSecond">The ticks per second.</param>
		public ObjectManager(IntPtr baseAddress, int capacity, int ticksPerSecond = 10) : base(baseAddress)
		{
			_capacity = capacity;
			_ticksPerSecond = ticksPerSecond;

			_log.Info($"ObjectManager initialized. Capacity = {capacity}, TPS = {ticksPerSecond}");
		}

		/// <summary>
		///     Gets the current objects in the game world.
		/// </summary>
		public IReadOnlyList<BaseEntity> Players => _players;

		internal LocalPlayer LocalPlayer { get; private set; }

		/// <summary>
		///     Updates the ObjectManager, obtaining all player entities from the game and adding them to the Players list.
		/// </summary>
		/// <exception cref="System.InvalidOperationException">
		///     Can not update the ObjectManager when it's not properly initialized!
		///     Are you sure BaseAddress is valid?
		/// </exception>
		public void Update()
		{
			if (!IsValid)
				throw new InvalidOperationException(
					"Can not update the ObjectManager when it's not properly initialized! Are you sure BaseAddress is valid?");

			// Throttle the updates a little - entities won't be changing that frequently.
			// Realistically we don't need to call this very often at all, as we only keep references to the actual
			// entities in the game, and only resolve their members when they're actually required.
			if (DateTime.Now - _lastUpdate <= TimeSpan.FromMilliseconds((double) 1000/_ticksPerSecond))
				return;

			if (!Orion.Client.InGame)
			{
				// No point in updating if we're not in game - we'd end up reading garbage.
				// Do set the last update time though, we especially don't want to tick too often in menu.
				_lastUpdate = DateTime.Now;
				return;
			}

			// Prevent duplicate entries - more efficient would be maintaining a dictionary and updating entities.
			// Then again, this is significantly less code, and performance wise not too big an impact. Leave it be for now,
			// but consider updating this in the future.
			_players.Clear();

			var localPlayerPtr = Orion.Memory.Read<IntPtr>(Orion.ClientBase + (int) BaseOffsets.LocalPlayer);

			LocalPlayer = new LocalPlayer(localPlayerPtr);

			// TODO: Actually get the num nodes in the entity list
			for (var i = 0; i < _capacity; i++)
			{
				_players.Add(new BaseEntity(GetEntityPtr(i)));
			}

			Trace.WriteLine($"[EntityManager] Update complete. {Players.Count(s => s.IsValid)} valid entries found.");


			_lastUpdate = DateTime.Now;
		}

		private IntPtr GetEntityPtr(int index)
		{
			// ptr = entityList + (idx * size)
			return Orion.Memory.Read<IntPtr>(BaseAddress + (index*(int) StaticOffsets.EntitySize));
		}

		/// <summary>
		///     Gets the player with the specified ID, and null if that player doesn't exist.
		/// </summary>
		/// <param name="id">The identifier.</param>
		/// <returns></returns>
		public BaseEntity GetPlayerById(int id)
		{
			if (_players.Count < id)
				return null;

			return Players.FirstOrDefault(p => p.Id == id);
		}
	}
}