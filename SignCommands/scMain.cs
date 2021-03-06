﻿using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using Newtonsoft.Json;
using Terraria;
using TShockAPI;
using Hooks;
using System.Text;
using System.Data;
using MySql.Data.MySqlClient;
using Mono.Data.Sqlite;
using TShockAPI.DB;
using System.Reflection;

namespace SignCommands
{
	[APIVersion(1, 12)]
	public class SignCommands : TerrariaPlugin
	{
		public static scConfig getConfig { get; set; }
		public static scPlayer[] scPlayers { get; set; }
		public static Dictionary<string, DateTime> GlobalCooldowns { get; set; }
		public static Dictionary<string, Dictionary<string, DateTime>> OfflineCooldowns { get; set; }
		public static bool UsingInfiniteSigns { get; set; }
		public static bool UsingVault { get; set; }

		DateTime lastCooldown { get; set; }
		DateTime lastPurge { get; set; }

		public SignCommands(Main game) : base(game)
		{
			getConfig = new scConfig();
			scPlayers = new scPlayer[256];
			GlobalCooldowns = new Dictionary<string, DateTime>();
			OfflineCooldowns = new Dictionary<string, Dictionary<string, DateTime>>();
			UsingInfiniteSigns = File.Exists(Path.Combine("ServerPlugins", "InfiniteSigns.dll"));
			UsingVault = File.Exists(Path.Combine("ServerPlugins", "Vault.dll"));
			this.lastCooldown = DateTime.UtcNow;
			this.lastPurge = DateTime.UtcNow;
			Order = -1;
		}

		public override string Name
		{
			get { return "Sign Commands"; }
		}

		public override string Author
		{
			get { return "by Scavenger"; }
		}

		public override string Description
		{
			get { return "Put commands on signs!"; }
		}

		public override Version Version
		{
			get { return Assembly.GetExecutingAssembly().GetName().Version; }
		}

		public override void Initialize()
		{
			GameHooks.Initialize += OnInitialize;
			if (!UsingInfiniteSigns)
				NetHooks.GetData += GetData;
			else
			{
				try { LoadDelegates(); }
				catch { }
			}
			NetHooks.GreetPlayer += OnGreetPlayer;
			ServerHooks.Leave += OnLeave;
			GameHooks.Update += OnUpdate;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				GameHooks.Initialize -= OnInitialize;
				if (!UsingInfiniteSigns)
					NetHooks.GetData -= GetData;
				else
				{
					try { UnloadDelegates(); }
					catch { }
				}
				NetHooks.GreetPlayer -= OnGreetPlayer;
				ServerHooks.Leave -= OnLeave;
				GameHooks.Update -= OnUpdate;
			}
			base.Dispose(disposing);
		}

		#region Load / Unload Delegates
		private void LoadDelegates()
		{
			InfiniteSigns.InfiniteSigns.SignEdit += OnSignEdit;
			InfiniteSigns.InfiniteSigns.SignHit += OnSignHit;
			InfiniteSigns.InfiniteSigns.SignKill += OnSignKill;
		}
		private void UnloadDelegates()
		{
			InfiniteSigns.InfiniteSigns.SignEdit -= OnSignEdit;
			InfiniteSigns.InfiniteSigns.SignHit -= OnSignHit;
			InfiniteSigns.InfiniteSigns.SignKill -= OnSignKill;
		}
		#endregion

		#region Initialize
		public void OnInitialize()
		{
			/* Add Commands */
			Commands.ChatCommands.Add(new Command("essentials.signs.break", CMDdestsign, "destsign"));
			Commands.ChatCommands.Add(new Command("essentials.signs.reload", CMDscreload, "screload"));

			/* Load Config */
			scConfig.LoadConfig();
		}
		#endregion

		#region Commands
		private void CMDdestsign(CommandArgs args)
		{
			scPlayer sPly = scPlayers[args.Player.Index];

			sPly.DestroyMode = true;
			args.Player.SendMessage("You can now destroy a sign!", Color.MediumSeaGreen);
		}

		private void CMDscreload(CommandArgs args)
		{
			scConfig.ReloadConfig(args);
		}
		#endregion

		#region scPlayers
		public void OnGreetPlayer(int who, HandledEventArgs e)
		{
			try
			{
				scPlayers[who] = new scPlayer(who);

				if (OfflineCooldowns.ContainsKey(TShock.Players[who].Name))
				{
					scPlayers[who].Cooldowns = OfflineCooldowns[TShock.Players[who].Name];
					OfflineCooldowns.Remove(TShock.Players[who].Name);
				}
			}
			catch { }
		}

		public void OnLeave(int who)
		{
			try
			{
				if (scPlayers[who] != null && scPlayers[who].Cooldowns.Count > 0)
					OfflineCooldowns.Add(TShock.Players[who].Name, scPlayers[who].Cooldowns);
				scPlayers[who] = null;
			}
			catch { }
		}
		#endregion

		#region Timer
		private void OnUpdate()
		{
			if ((DateTime.UtcNow - lastCooldown).TotalMilliseconds >= 1000)
			{
				lastCooldown = DateTime.UtcNow;
				try
				{
					foreach (var sPly in scPlayers)
					{
						if (sPly == null) continue;
						if (sPly.AlertCooldownCooldown > 0)
							sPly.AlertCooldownCooldown--;
						if (sPly.AlertPermissionCooldown > 0)
							sPly.AlertPermissionCooldown--;
						if (sPly.AlertDestroyCooldown > 0)
							sPly.AlertDestroyCooldown--;
					}

					if ((DateTime.UtcNow - lastPurge).TotalMinutes >= 5)
					{
						lastPurge = DateTime.UtcNow;

						List<string> CooldownGroups = new List<string>(GlobalCooldowns.Keys);
						foreach (string g in CooldownGroups)
						{
							if (DateTime.UtcNow > GlobalCooldowns[g])
								GlobalCooldowns.Remove(g);
						}

						List<string> OfflinePlayers = new List<string>(OfflineCooldowns.Keys);
						foreach (string p in OfflinePlayers)
						{
							List<string> OfflinePlayerCooldowns = new List<string>(OfflineCooldowns[p].Keys);
							foreach (string g in OfflinePlayerCooldowns)
							{
								if (DateTime.UtcNow > OfflineCooldowns[p][g])
									OfflineCooldowns[p].Remove(g);
							}
							if (OfflineCooldowns[p].Count == 0)
								OfflineCooldowns.Remove(p);
						}

						foreach (var sPly in scPlayers)
						{
							if (sPly == null) continue;
							List<string> CooldownIds = new List<string>(sPly.Cooldowns.Keys);
							foreach (string id in CooldownIds)
							{
								if (DateTime.UtcNow > sPly.Cooldowns[id])
									sPly.Cooldowns.Remove(id);
							}
						}
					}

				}
				catch { }
			}
		}
		#endregion

		#region OnSignEdit
		private void OnSignEdit(InfiniteSigns.SignEventArgs args)
		{
			try
			{
				if (args.Handled) return;
				args.Handled = OnSignEdit(args.X, args.Y, args.text, args.Who);
			}
			catch { }
		}
		private bool OnSignEdit(int X, int Y, string text, int who)
		{
			if (!text.ToLower().StartsWith(getConfig.DefineSignCommands.ToLower())) return false;

			TSPlayer tPly = TShock.Players[who];
			scSign sign = new scSign(text, new Point(X, Y));

			if (scUtils.CanCreate(tPly, sign)) return false;

			tPly.SendMessage("You do not have permission to create that sign command!", Color.IndianRed);
			return true;
		}
		#endregion
		
		#region OnSignHit
		private void OnSignHit(InfiniteSigns.SignEventArgs args)
		{
			try
			{
				if (args.Handled) return;
				args.Handled = OnSignHit(args.X, args.Y, args.text, args.Who);
			}
			catch { }
		}
		private bool OnSignHit(int X, int Y, string text, int who)
		{
			if (!text.ToLower().StartsWith(getConfig.DefineSignCommands.ToLower())) return false;
			TSPlayer tPly = TShock.Players[who];
			scPlayer sPly = scPlayers[who];
			scSign sign = new scSign(text, new Point(X, Y));

			bool CanBreak = scUtils.CanBreak(tPly, sign);
			if (sPly.DestroyMode && CanBreak) return false;

			if (getConfig.ShowDestroyMessage && CanBreak && sPly.AlertDestroyCooldown == 0)
			{
				tPly.SendMessage("To destroy this sign, Type \"/destsign\"", Color.Orange);
				sPly.AlertDestroyCooldown = 10;
			}

			sign.ExecuteCommands(sPly);

			return true;
		}
		#endregion

		#region OnSignKill
		private void OnSignKill(InfiniteSigns.SignEventArgs args)
		{
			try
			{
				if (args.Handled) return;
				args.Handled = OnSignKill(args.X, args.Y, args.text, args.Who);
			}
			catch { }
		}
		private bool OnSignKill(int X, int Y, string text, int who)
		{
			if (!text.ToLower().StartsWith(getConfig.DefineSignCommands.ToLower())) return false;

			var sPly = scPlayers[who];
			scSign sign = new scSign(text, new Point(X, Y));

			if (sPly.DestroyMode && scUtils.CanBreak(sPly.TSPlayer, sign))
			{
				sPly.DestroyMode = false;
				string id = new Point(X, Y).ToString();
				List<string> OfflinePlayers = new List<string>(OfflineCooldowns.Keys);
				foreach (var p in OfflinePlayers)
				{
					if (OfflineCooldowns[p].ContainsKey(id))
						OfflineCooldowns[p].Remove(id);
				}

				foreach (var Ply in scPlayers)
				{
					if (Ply == null || !Ply.Cooldowns.ContainsKey(id)) continue;
					Ply.Cooldowns.Remove(id);
				}
				return false;
			}
			sign.ExecuteCommands(sPly);
			return true;
		}
		#endregion

		#region GetData
		public void GetData(GetDataEventArgs e)
		{
			try
			{
				if (e.Handled || UsingInfiniteSigns) return;
				switch (e.MsgID)
				{
					#region Sign Edit
					case PacketTypes.SignNew:
						{
							int X = BitConverter.ToInt32(e.Msg.readBuffer, e.Index + 2);
							int Y = BitConverter.ToInt32(e.Msg.readBuffer, e.Index + 6);
							string text = Encoding.UTF8.GetString(e.Msg.readBuffer, e.Index + 10, e.Length - 11);

							int id = Terraria.Sign.ReadSign(X, Y);
							if (id < 0 || Main.sign[id] == null) return;
							X = Main.sign[id].x;
							Y = Main.sign[id].y;
							if (OnSignEdit(X, Y, text, e.Msg.whoAmI))
							{
								e.Handled = true;
								TShock.Players[e.Msg.whoAmI].SendData(PacketTypes.SignNew, "", id);
							}
						}
						break;
					#endregion

					#region Tile Modify
					case PacketTypes.Tile:
						{
							int X = BitConverter.ToInt32(e.Msg.readBuffer, e.Index + 1);
							int Y = BitConverter.ToInt32(e.Msg.readBuffer, e.Index + 5);

							if (Main.tile[X, Y].type != 55) return;

							int id = Terraria.Sign.ReadSign(X, Y);
							if (id < 0 || Main.sign[id] == null) return;
							X = Main.sign[id].x;
							Y = Main.sign[id].y;
							string text = Main.sign[id].text;

							bool handle = false;
							if (e.Msg.readBuffer[e.Index] == 0 && e.Msg.readBuffer[e.Index + 9] == 0)
								handle = OnSignKill(X, Y, text, e.Msg.whoAmI);
							else
								handle = OnSignHit(X, Y, text, e.Msg.whoAmI);

							if (handle)
							{
								e.Handled = true;
								TShock.Players[e.Msg.whoAmI].SendTileSquare(X, Y);
							}
						}
						break;
					#endregion
				}
			}
			catch (Exception ex)
			{
				Log.Error("[Sign Commands] Exception:");
				Log.Error(ex.ToString());
			}
		}
		#endregion
	}
}