﻿using System.Collections.Generic;
using MySql.Data.MySqlClient;
using System.ComponentModel;
using System.Reflection;
using Newtonsoft.Json;
using System.Drawing;
using System.Timers;
using TShockAPI.DB;
using System.Text;
using System.Data;
using TShockAPI;
using System.IO;
using Terraria;
using System;
using Hooks;

namespace Essentials
{
    [APIVersion(1, 11)]
    public class Essentials : TerrariaPlugin
    {
        #region Variables
        public static List<esPlayer> esPlayers = new List<esPlayer>();
        public static SqlTableEditor SQLEditor;
        public static SqlTableCreator SQLWriter;
        public static Timer CheckT = new Timer(1000);
        public static int playercount = 0;
        public static bool useteamperms = false;
        public static string redpassword = "";
        public static string greenpassword = "";
        public static string bluepassword = "";
        public static string yellowpassword = "";
        #endregion

        #region Plugin Main
        public override string Name
        {
            get { return "Essentials"; }
        }

        public override string Author
        {
            get { return "by Scavenger"; }
        }

        public override string Description
        {
            get { return "some Essential commands for TShock!"; }
        }

        public override Version Version
        {
            get { return new Version("1.3.2"); }
        }

        public override void Initialize()
        {
            GameHooks.Initialize += OnInitialize;
            NetHooks.GreetPlayer += OnGreetPlayer;
            ServerHooks.Leave += OnLeave;
            ServerHooks.Chat += OnChat;
            NetHooks.GetData += GetData;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GameHooks.Initialize -= OnInitialize;
                NetHooks.GreetPlayer -= OnGreetPlayer;
                ServerHooks.Leave -= OnLeave;
                ServerHooks.Chat -= OnChat;
                NetHooks.GetData -= GetData;
            }
            base.Dispose(disposing);
        }

        public Essentials(Main game)
            : base(game)
        {
            Order = -1;
        }
        #endregion

        #region Hooks
        public void OnInitialize()
        {
            SQLEditor = new SqlTableEditor(TShock.DB, TShock.DB.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
            SQLWriter = new SqlTableCreator(TShock.DB, TShock.DB.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());

            var table = new SqlTable("EssentialsUserHomes",
                new SqlColumn("UserID", MySqlDbType.Int32) { Unique = true },
                new SqlColumn("HomeX", MySqlDbType.Int32),
                new SqlColumn("HomeY", MySqlDbType.Int32),
                new SqlColumn("WorldID", MySqlDbType.Int32)
            );
            SQLWriter.EnsureExists(table);

            CheckT.Elapsed += new ElapsedEventHandler(CheckT_Elapsed);
            if (playercount > 0)
                CheckT.Start();

            if (!File.Exists(@"tshock/EssentialsConfig.txt"))
            {
                File.WriteAllText(@"tshock/EssentialsConfig.txt", "#> < is a comment" + Environment.NewLine +
                    "#> To Completely disable locking of teams, set LockTeamsWithPermissions to false and set the passwords to nothing" + Environment.NewLine +
                    "#> Lock Teams with Permissions or passwords (Boolean):" + Environment.NewLine +
                    "LockTeamsWithPermissions:false" + Environment.NewLine +
                    "#> Passwords for teams (leave blank for no password) Make sure the passowrd is in quotes \"<password>\" (String):" + Environment.NewLine +
                    "RedPassword:\"\"" + Environment.NewLine +
                    "GreenPassword:\"\"" + Environment.NewLine +
                    "BluePassword:\"\"" + Environment.NewLine +
                    "YellowPassword:\"\"" + Environment.NewLine);
                useteamperms = false;
                redpassword = "";
                greenpassword = "";
                bluepassword = "";
                yellowpassword = "";
            }
            else
            {
                using (StreamReader file = new StreamReader(@"tshock/EssentialsConfig.txt", true))
                {
                    string[] rFile = (file.ReadToEnd()).Split('\n');
                    foreach (string currentLine in rFile)
                    {
                        try
                        {
                            if (currentLine.StartsWith("LockTeamsWithPermissions:"))
                            {
                                string tempLine = currentLine;
                                tempLine = tempLine.Remove(0, 25);
                                if (tempLine.StartsWith("false"))
                                    useteamperms = false;
                                else if (tempLine.StartsWith("true"))
                                    useteamperms = true;
                                else
                                {
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine("Error in Essentials config file - LockTeamsWithPermissions");
                                    Console.ForegroundColor = ConsoleColor.Gray;
                                }
                            }
                            else if (currentLine.StartsWith("RedPassword:"))
                            {
                                string tempLine = currentLine;
                                tempLine = tempLine.Remove(0, 12);
                                tempLine = tempLine.Split('\"', '\'')[1];
                                redpassword = tempLine;
                            }
                            else if (currentLine.StartsWith("GreenPassword:"))
                            {
                                string tempLine = currentLine;
                                tempLine = tempLine.Remove(0, 14);
                                tempLine = tempLine.Split('\"', '\'')[1];
                                greenpassword = tempLine;
                            }
                            else if (currentLine.StartsWith("BluePassword:"))
                            {
                                string tempLine = currentLine;
                                tempLine = tempLine.Remove(0, 13);
                                tempLine = tempLine.Split('\"', '\'')[1];
                                bluepassword = tempLine;
                            }
                            else if (currentLine.StartsWith("YellowPassword:"))
                            {
                                string tempLine = currentLine;
                                tempLine = tempLine.Remove(0, 15);
                                tempLine = tempLine.Split('\"', '\'')[1];
                                yellowpassword = tempLine;
                            }
                        }
                        catch (Exception)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Error in Essentials config file - TeamPasswords", Color.IndianRed);
                            Console.ForegroundColor = ConsoleColor.Gray;
                            return;
                        }
                    }
                }
            }

            Commands.ChatCommands.Add(new Command("fillstacks", more, "maxstacks"));
            Commands.ChatCommands.Add(new Command("getposition", getpos, "pos"));
            Commands.ChatCommands.Add(new Command("tp", tppos, "tppos"));
            Commands.ChatCommands.Add(new Command("ruler", ruler, "ruler"));
            Commands.ChatCommands.Add(new Command("askadminhelp", helpop, "helpop"));
            Commands.ChatCommands.Add(new Command("commitsuicide", suicide, "suicide", "die"));
            Commands.ChatCommands.Add(new Command("setonfire", burn, "burn"));
            Commands.ChatCommands.Add(new Command("butcher", killnpc, "killnpc"));
            Commands.ChatCommands.Add(new Command("kickall", kickall, "kickall"));
            Commands.ChatCommands.Add(new Command("moonphase", moon, "moon"));
            Commands.ChatCommands.Add(new Command("convertbiomes", cbiome, "cbiome", "bconvert"));
            Commands.ChatCommands.Add(new Command("searchids", sitems, "sitem", "si", "searchitem"));
            Commands.ChatCommands.Add(new Command("searchids", spage, "spage", "sp"));
            Commands.ChatCommands.Add(new Command("searchids", snpcs, "snpc", "sn", "searchnpc"));
            Commands.ChatCommands.Add(new Command("myhome", setmyhome, "sethome"));
            Commands.ChatCommands.Add(new Command("myhome", gomyhome, "myhome"));
            Commands.ChatCommands.Add(new Command("essentials", cmdessentials, "essentials"));
            Commands.ChatCommands.Add(new Command(null, TeamUnlock, "teamunlock"));

            foreach (Group grp in TShock.Groups.groups)
            {
                if (grp.Name != "superadmin" && grp.HasPermission("backondeath"))
                    grp.AddPermission("backontp");   
            }
            Commands.ChatCommands.Add(new Command("backontp", back, "b"));
        }

        public void OnGreetPlayer(int who, HandledEventArgs e)
        {
            lock (esPlayers)
                esPlayers.Add(new esPlayer(who));

            playercount++;
            if (playercount == 1)
                CheckT.Start();
        }

        public void OnLeave(int ply)
        {
            lock (esPlayers)
            {
                for (int i = 0; i < esPlayers.Count; i++)
                {
                    if (esPlayers[i].Index == ply)
                    {
                        esPlayers.RemoveAt(i);
                        break;
                    }
                }
            }
            playercount--;
            if (playercount == 0)
                CheckT.Stop();
        }

        public void OnChat(messageBuffer msg, int ply, string text, HandledEventArgs e)
        {
            if (e.Handled)
                return;

            if (text == "/")
                e.Handled = true;

            if (text.StartsWith("/tp "))
            {
                #region /tp
                var player = TShock.Players[ply];
                if (player.Group.HasPermission("tp") && player.RealPlayer)
                {

                    List<string> parms = new List<string>();
                    string[] texts = text.Split(' ');
                    for (int i = 1; i < texts.Length; i++)
                    {
                        parms.Add(texts[i]);
                    }

                    string plStr = String.Join(" ", parms);
                    var players = TShock.Utils.FindPlayer(plStr);

                    if (parms.Count > 0 && players.Count == 1 && players[0].TPAllow && player.Group.HasPermission(Permissions.tpall))
                    {
                        esPlayer play = GetesPlayerByName(player.Name);
                        play.lastXtp = player.TileX;
                        play.lastYtp = player.TileY;
                        play.lastaction = "tp";
                    }
                }
                #endregion
            }
            else if (text.StartsWith("/home"))
            {
                #region /home
                var player = TShock.Players[ply];
                if (player.Group.HasPermission("tp") && player.RealPlayer)
                {
                    esPlayer play = GetesPlayerByName(player.Name);
                    play.lastXtp = player.TileX;
                    play.lastYtp = player.TileY;
                    play.lastaction = "tp";
                }
                #endregion
            }
            else if (text.StartsWith("/spawn"))
            {
                #region /spawn
                var player = TShock.Players[ply];
                if (player.Group.HasPermission("tp") && player.RealPlayer)
                {
                    esPlayer play = GetesPlayerByName(player.Name);
                    play.lastXtp = player.TileX;
                    play.lastYtp = player.TileY;
                    play.lastaction = "tp";
                }
                #endregion
            }
            else if (text.StartsWith("/warp "))
            {
                #region /warp
                var player = TShock.Players[ply];
                if (player.Group.HasPermission("warp"))
                {
                    List<string> parms = new List<string>();
                    string[] textsp = text.Split(' ');
                    for (int i = 1; i < textsp.Length; i++)
                    {
                        parms.Add(textsp[i]);
                    }

                    if (parms.Count > 0 && !parms[0].Equals("list"))
                    {
                        string warpName = String.Join(" ", parms);
                        var warp = TShock.Warps.FindWarp(warpName);
                        if (warp.WarpPos != Vector2.Zero)
                        {
                            esPlayer play = GetesPlayerByName(player.Name);
                            play.lastXtp = player.TileX;
                            play.lastYtp = player.TileY;
                            play.lastaction = "tp";
                        }
                    }
                }
                #endregion
            }
        }

        public void GetData(GetDataEventArgs e)
        {
            try
            {
                switch (e.MsgID)
                {
                    case PacketTypes.PlayerTeam:
                        using (var data = new MemoryStream(e.Msg.readBuffer, e.Index, e.Length))
                        {
                            var reader = new BinaryReader(data);
                            var play = reader.ReadByte();
                            var team = reader.ReadByte();
                            esPlayer ply = GetesPlayerByID(play);
                            var tply = TShock.Players[play];
                            switch (team)
                            {

                                case 1: if (((useteamperms && !tply.Group.HasPermission("jointeamred")) || (!useteamperms && ply.redpass != redpassword)) && tply.Group.Name != "superadmin")
                                    {

                                        e.Handled = true;
                                        if (useteamperms)
                                            tply.SendMessage("You do not have permission to join this team.", Color.Red);
                                        else
                                            tply.SendMessage("This team is locked, use /teamunlock red <password> to access it.", Color.Red);
                                        tply.SetTeam(tply.Team);

                                    } break;
                                case 2: if (((useteamperms && !tply.Group.HasPermission("jointeamgreen")) || (!useteamperms && ply.greenpass != greenpassword)) && tply.Group.Name != "superadmin")
                                    {

                                        e.Handled = true;
                                        if (useteamperms)
                                            tply.SendMessage("You do not have permission to join this team.", Color.Red);
                                        else
                                            tply.SendMessage("This team is locked, use /teamunlock green <password> to access it.", Color.Red);
                                        tply.SetTeam(tply.Team);

                                    } break;
                                case 3: if (((useteamperms && !tply.Group.HasPermission("jointeamblue")) || (!useteamperms && ply.bluepass != bluepassword)) && tply.Group.Name != "superadmin")
                                    {
                                        
                                        e.Handled = true;
                                        if (useteamperms)
                                            tply.SendMessage("You do not have permission to join this team.", Color.Red);
                                        else
                                            tply.SendMessage("This team is locked, use /teamunlock blue <password> to access it.", Color.Red);
                                        tply.SetTeam(tply.Team);

                                    } break;
                                case 4: if (((useteamperms && !tply.Group.HasPermission("jointeamyellow")) || (!useteamperms && ply.yellowpass != yellowpassword)) && tply.Group.Name != "superadmin")
                                    {

                                        e.Handled = true;
                                        if (useteamperms)
                                            tply.SendMessage("You do not have permission to join this team.", Color.Red);
                                        else
                                            tply.SendMessage("This team is locked, use /teamunlock yellow <password> to access it.", Color.Red);
                                        tply.SetTeam(tply.Team);

                                    } break;

                            }
                        }
                        break;
                }
            }
            catch (Exception) { }
        }

        #endregion

        #region Timer
        static void CheckT_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                lock (esPlayers)
                {
                    foreach (esPlayer play in esPlayers)
                    {
                        if (play.TSPlayer.Dead && !play.ondeath && play != null)
                        {
                            if (play.grpData.HasPermission("backondeath"))
                            {
                                play.lastXondeath = play.TSPlayer.TileX;
                                play.lastYondeath = play.TSPlayer.TileY;
                                play.SendMessage("Type \"/b\" to return to your position before you died", Color.MediumSeaGreen);
                                play.ondeath = true;
                                play.lastaction = "death";
                            }
                        }
                        else if (!play.TSPlayer.Dead && play.ondeath && play != null)
                        {
                            play.ondeath = false;
                        }
                    }
                }
            }
            catch (Exception) { }
        }
        #endregion

        #region Methods
        //GET esPlayer!
        public static esPlayer GetesPlayerByID(int id)
        {
            esPlayer player = null;
            foreach (esPlayer ply in esPlayers)
            {
                if (ply.Index == id)
                    return ply;
            }
            return player;
        }

        public static esPlayer GetesPlayerByName(string name)
        {
            var player = TShock.Utils.FindPlayer(name)[0];
            if (player != null)
            {
                foreach (esPlayer ply in esPlayers)
                {
                    if (ply.TSPlayer == player)
                        return ply;
                }
            }
            return null;
        }

        //HELPOP - BC to Admin
        public static void BroadcastToAdmin(CommandArgs plrsent, string msgtosend)
        {
            plrsent.Player.SendMessage("To Admins> " + plrsent.Player.Name + ": " + msgtosend, Color.RoyalBlue);
            foreach (esPlayer player in esPlayers)
            {
                if (player.grpData.HasPermission("recieveadminhelp"))
                    player.SendMessage("[HO] " + plrsent.Player.Name + ": " + msgtosend, Color.RoyalBlue);
            }
        }

        //SEACH IDs
        public static List<Item> GetItemByName(string name)
        {
            var found = new List<Item>();
            for (int i = -24; i < Main.maxItemTypes; i++)
            {
                try
                {
                    Item item = new Item();
                    item.netDefaults(i);
                    if (item.name.ToLower().Contains(name.ToLower()))
                        found.Add(item);
                }
                catch { }
            }
            return found;
        }

        public static List<NPC> GetNPCByName(string name)
        {
            var found = new List<NPC>();
            for (int i = 1; i < Main.maxNPCTypes; i++)
            {
                NPC npc = new NPC();
                npc.netDefaults(i);
                if (npc.name.ToLower().Contains(name.ToLower()))
                    found.Add(npc);
            }
            return found;
        }

        //^String Builder
        public static void BCsearchitem(CommandArgs args, List<Item> list, int page)
        {
            args.Player.SendMessage("Item Search:", Color.Yellow);
            var sb = new StringBuilder();
            if (list.Count > (8 * (page - 1)))
            {
                for (int j = (8 * (page - 1)); j < (8 * page); j++)
                {
                    if (sb.Length != 0)
                        sb.Append(" | ");
                    sb.Append(list[j].netID).Append(": ").Append(list[j].name);
                    if (j == list.Count - 1)
                    {
                        args.Player.SendMessage(sb.ToString(), Color.MediumSeaGreen);
                        break;
                    }
                    if ((j + 1) % 2 == 0)
                    {
                        args.Player.SendMessage(sb.ToString(), Color.MediumSeaGreen);
                        sb.Clear();
                    }
                }
            }
            if (list.Count > (8 * page))
            {
                args.Player.SendMessage(string.Format("Type /spage {0} for more Results.", (page + 1)), Color.Yellow);
            }
        }

        public static void BCsearchnpc(CommandArgs args, List<NPC> list, int page)
        {
            args.Player.SendMessage("NPC Search:", Color.Yellow);
            var sb = new StringBuilder();
            if (list.Count > (8 * (page - 1)))
            {
                for (int j = (8 * (page - 1)); j < (8 * page); j++)
                {
                    if (sb.Length != 0)
                        sb.Append(" | ");
                    sb.Append(list[j].netID).Append(": ").Append(list[j].name);
                    if (j == list.Count - 1)
                    {
                        args.Player.SendMessage(sb.ToString(), Color.MediumSeaGreen);
                        break;
                    }
                    if ((j + 1) % 2 == 0)
                    {
                        args.Player.SendMessage(sb.ToString(), Color.MediumSeaGreen);
                        sb.Clear();
                    }
                }
            }
            if (list.Count > (8 * page))
            {
                args.Player.SendMessage(string.Format("Type /spage {0} for more Results.", (page + 1)), Color.Yellow);
            }
        }
        #endregion

        //Commands:

        #region Fill Items
        public static void more(CommandArgs args)
        {
            int i = 0;
            foreach (Item item in args.TPlayer.inventory)
            {
                int togive = item.maxStack - item.stack;
                if (item.stack != 0 && i <= 39)
                    args.Player.GiveItem(item.type, item.name, item.width, item.height, togive);
                i++;
            }
        }
        #endregion

        #region Position Commands
        public static void getpos(CommandArgs args)
        {
            args.Player.SendMessage("X Position: " + args.Player.TileX + " - Y Position: " + args.Player.TileY, Color.Yellow);

        }

        public static void tppos(CommandArgs args)
        {
            if (args.Parameters.Count != 2)
                args.Player.SendMessage("Format is: /tppos <X> <Y>", Color.Red);
            else
            {
                int xcord = 0;
                int ycord = 0;
                int.TryParse(args.Parameters[0], out xcord);
                int.TryParse(args.Parameters[1], out ycord);
                esPlayer play = GetesPlayerByName(args.Player.Name);
                play.lastXtp = args.Player.TileX;
                play.lastYtp = args.Player.TileY;
                play.lastaction = "tp";
                if (args.Player.Teleport(xcord, ycord))
                    args.Player.SendMessage("Teleported you to X: " + xcord + " - Y: " + ycord, Color.MediumSeaGreen);
            }
        }

        public static void ruler(CommandArgs args)
        {
            int choice = 0;

            if (args.Parameters.Count == 1 &&
                int.TryParse(args.Parameters[0], out choice) &&
                choice >= 1 && choice <= 2)
            {
                args.Player.SendMessage("Hit a block to Set Point " + choice, Color.Yellow);
                args.Player.AwaitingTempPoint = choice;           
            }
            else
            {
                if (args.Player.TempPoints[0] == Point.Zero || args.Player.TempPoints[1] == Point.Zero)
                    args.Player.SendMessage("Invalid Points! To set points use: /ruler [1/2]", Color.Red);
                else
                {
                    var width = Math.Abs(args.Player.TempPoints[0].X - args.Player.TempPoints[1].X);
                    var height = Math.Abs(args.Player.TempPoints[0].Y - args.Player.TempPoints[1].Y);
                    args.Player.SendMessage("Area Height: " + height + " Width: " + width, Color.LightGreen);
                    args.Player.TempPoints[0] = Point.Zero; args.Player.TempPoints[1] = Point.Zero;
                }
            }
        }
        #endregion

        #region Help OP
        public static void helpop(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendMessage("Usage: /helpop <message>", Color.Red);
                return;
            }

            if (args.Parameters.Count > 0)
            {
                string text = "";

                foreach (string word in args.Parameters)
                {
                    text = text + word + " ";
                }

                BroadcastToAdmin(args, text);
            }
            else
            {
                args.Player.SendMessage("Usage: /helpop <message>", Color.Red);
            }

        }
        #endregion

        #region Suicide
        public static void suicide(CommandArgs args)
        {
            if (!args.Player.RealPlayer)
                return;

            args.Player.DamagePlayer(9999);
        }

        public static void burn(CommandArgs args)
        {
            int duration = 1800;
            foreach (string parameter in args.Parameters)
            {
                int isduration = 0;
                bool IsaNum = int.TryParse(parameter, out isduration);
                if (IsaNum)
                    duration = isduration * 60;
            }

            var player = TShock.Utils.FindPlayer(args.Parameters[0]);
            if (player.Count == 0)
                args.Player.SendMessage("Invalid player!", Color.Red);
            else if (player.Count > 1)
                args.Player.SendMessage("More than one player matched!", Color.Red);
            else
            {
                player[0].SetBuff(24, duration);
                args.Player.SendMessage(player[0].Name + " Has been set on fire! for " + (duration / 60) + " seconds", Color.MediumSeaGreen);
            }
        }
        #endregion

        #region killnpc
        public static void killnpc(CommandArgs args)
        {
            if (args.Parameters.Count != 0)
            {

                var npcselected = TShock.Utils.GetNPCByIdOrName(args.Parameters[0]);
                if (npcselected.Count == 0)
                    args.Player.SendMessage("Invalid NPC!", Color.Red);
                else if (npcselected.Count > 1)
                    args.Player.SendMessage("More than one NPC matched!", Color.Red);
                else
                {
                    int killcount = 0;
                    for (int i = 0; i < Main.npc.Length; i++)
                    {
                        if (Main.npc[i].active && Main.npc[i].type != 0 && Main.npc[i].name == npcselected[0].name)
                        {
                            TSPlayer.Server.StrikeNPC(i, 99999, 90f, 1);
                            killcount++;
                        }
                    }
                    args.Player.SendMessage("Killed " + killcount + " " + npcselected[0].name + "!", Color.MediumSeaGreen);
                }
            }
            else
            {
                int killcount = 0;
                for (int i = 0; i < Main.npc.Length; i++)
                {
                    if (Main.npc[i].active && Main.npc[i].type != 0 && !Main.npc[i].townNPC && !Main.npc[i].friendly)
                    {
                        TSPlayer.Server.StrikeNPC(i, 99999, 90f, 1);
                        killcount++;
                    }
                }
                args.Player.SendMessage("Killed " + killcount + " NPCs.", Color.MediumSeaGreen);
            }
        }
        #endregion

        #region Kickall
        public static void kickall(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                foreach (esPlayer player in esPlayers)
                {
                    if (!player.grpData.HasPermission("immunetokickall"))
                    {
                        player.Kick("Everyone has been kicked from the server!");
                        TShock.Utils.Broadcast("Everyone has been kicked from the server");
                    }
                }
            }

            if (args.Parameters.Count > 0)
            {
                string text = "";

                foreach (string word in args.Parameters)
                    text = text + word + " ";

                foreach (esPlayer player in esPlayers)
                {
                    if (!player.grpData.HasPermission("immunetokickall"))
                        player.Kick("Everyone has been kicked (" + text + ")");
                }
                TShock.Utils.Broadcast("Everyone has been kicked from the server!");
            }
            else
            {
                foreach (esPlayer player in esPlayers)
                {
                    if (!player.grpData.HasPermission("immunetokickall"))
                        player.Kick("Everyone has been kicked from the server!");
                }
                TShock.Utils.Broadcast("Everyone has been kicked from the server!");
            }
        }
        #endregion

        #region Moon Phase
        public static void moon(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendMessage("Usage: /moon [ new | 1/4 | half | 3/4 | full ]", Color.OrangeRed);
                return;
            }

            string subcmd = args.Parameters[0].ToLower();

            if (subcmd == "new")
            {
                Main.moonPhase = 4;
                args.Player.SendMessage("Moon Phase set to New Moon, This takes a while to update!", Color.MediumSeaGreen);
            }
            else if (subcmd == "1/4")
            {
                Main.moonPhase = 3;
                args.Player.SendMessage("Moon Phase set to 1/4 Moon, This takes a while to update!", Color.MediumSeaGreen);
            }
            else if (subcmd == "half")
            {
                Main.moonPhase = 2;
                args.Player.SendMessage("Moon Phase set to Half Moon, This takes a while to update!", Color.MediumSeaGreen);
            }
            else if (subcmd == "3/4")
            {
                Main.moonPhase = 1;
                args.Player.SendMessage("Moon Phase set to 3/4 Moon, This takes a while to update!", Color.MediumSeaGreen);
            }
            else if (subcmd == "full")
            {
                Main.moonPhase = 0;
                args.Player.SendMessage("Moon Phase set to Full Moon, This takes a while to update!", Color.MediumSeaGreen);
            }
            else
                args.Player.SendMessage("Usage: /moon [ new | 1/4 | half | 3/4 | full ]", Color.OrangeRed);
        }
        #endregion

        #region Back
        private static void back(CommandArgs args)
        {
            esPlayer play = GetesPlayerByName(args.Player.Name);
            if (play.lastaction == "none")
                args.Player.SendMessage("You do not have a /b position stored", Color.MediumSeaGreen);
            else if (play.lastaction == "death")
            {
                int Xdeath = play.lastXondeath;
                int Ydeath = play.lastYondeath;
                if (args.Player.Teleport(Xdeath, Ydeath))
                    args.Player.SendMessage("Moved you to your position before you died!", Color.MediumSeaGreen);
            }
            else if (play.grpData.HasPermission("backontp"))
            {
                int Xtp = play.lastXtp;
                int Ytp = play.lastYtp;
                if (args.Player.Teleport(Xtp, Ytp))
                    args.Player.SendMessage("Moved you to your position before you last teleported", Color.MediumSeaGreen);
            }
            else if (play.lastaction == "death" && !play.grpData.HasPermission("backondeath"))
            {
                args.Player.SendMessage("You do not have permission to /b after death", Color.MediumSeaGreen);
            }
        }
        #endregion

        #region Convert Biomes
        public static void cbiome(CommandArgs args)
        {
            if (args.Parameters.Count < 2 || args.Parameters.Count > 3)
            {
                args.Player.SendMessage("Usage: /cbiome <from> <to> [region]", Color.IndianRed);
                args.Player.SendMessage("Possible Biomes: Corruption, Hallow, Normal", Color.IndianRed);
                return;
            }

            string from = args.Parameters[0].ToLower();
            string to = args.Parameters[1].ToLower();
            string region = "";
            var regiondata = TShock.Regions.GetRegionByName("");
            bool doregion = false;

            if (args.Parameters.Count == 3)
            {
                region = args.Parameters[2];
                if (TShock.Regions.ZacksGetRegionByName(region) != null)
                {
                    doregion = true;
                    regiondata = TShock.Regions.GetRegionByName(region);
                }
            }


            if (from == "normal")
            {
                if (!doregion)
                    args.Player.SendMessage("You must specify a valid region to convert a normal biome.", Color.IndianRed);
                else if (to == "normal")
                    args.Player.SendMessage("You cannot convert Normal to Normal.", Color.IndianRed);
                else if (to == "hallow" && doregion)
                {
                    args.Player.SendMessage("Server might lag for a moment.", Color.IndianRed);
                    for (int x = 0; x < Main.maxTilesX; x++)
                    {
                        for (int y = 0; y < Main.maxTilesY; y++)
                        {
                            if (doregion && x >= regiondata.Area.Left && x <= regiondata.Area.Right && y >= regiondata.Area.Top && y <= regiondata.Area.Bottom)
                            {
                                switch (Main.tile[x, y].type)
                                {
                                    case 1:
                                        Main.tile[x, y].type = 117;
                                        break;
                                    case 2:
                                        Main.tile[x, y].type = 109;
                                        break;
                                    case 53:
                                        Main.tile[x, y].type = 116;
                                        break;
                                    case 3:
                                        Main.tile[x, y].type = 110;
                                        break;
                                    case 73:
                                        Main.tile[x, y].type = 113;
                                        break;
                                    case 52:
                                        Main.tile[x, y].type = 115;
                                        break;
                                    default:
                                        continue;
                                }
                            }
                        }
                    }
                    WorldGen.CountTiles(0);
                    TSPlayer.All.SendData(PacketTypes.UpdateGoodEvil);
                    Netplay.ResetSections();
                    args.Player.SendMessage("Converted Normal into Hallow!", Color.MediumSeaGreen);
                }
                else if (to == "corruption" && doregion)
                {
                    args.Player.SendMessage("Server might lag for a moment.", Color.IndianRed);
                    for (int x = 0; x < Main.maxTilesX; x++)
                    {
                        for (int y = 0; y < Main.maxTilesY; y++)
                        {
                            if (doregion && x >= regiondata.Area.Left && x <= regiondata.Area.Right && y >= regiondata.Area.Top && y <= regiondata.Area.Bottom)
                            {
                                switch (Main.tile[x, y].type)
                                {
                                    case 1:
                                        Main.tile[x, y].type = 25;
                                        break;
                                    case 2:
                                        Main.tile[x, y].type = 23;
                                        break;
                                    case 53:
                                        Main.tile[x, y].type = 112;
                                        break;
                                    case 3:
                                        Main.tile[x, y].type = 24;
                                        break;
                                    case 73:
                                        Main.tile[x, y].type = 24;
                                        break;
                                    default:
                                        continue;
                                }
                            }
                        }
                    }
                    WorldGen.CountTiles(0);
                    TSPlayer.All.SendData(PacketTypes.UpdateGoodEvil);
                    Netplay.ResetSections();
                    args.Player.SendMessage("Converted Normal into Corruption!", Color.MediumSeaGreen);
                }
            }
            else if (from == "hallow")
            {
                if (args.Parameters.Count == 3 && !doregion)
                    args.Player.SendMessage("You must specify a valid region to convert a normal biome.", Color.IndianRed);
                else if (to == "hallow")
                    args.Player.SendMessage("You cannot convert Hallow to hallow.", Color.IndianRed);
                else if (to == "corruption")
                {
                    args.Player.SendMessage("Server might lag for a moment.", Color.IndianRed);
                    for (int x = 0; x < Main.maxTilesX; x++)
                    {
                        for (int y = 0; y < Main.maxTilesY; y++)
                        {
                            if (!doregion || (doregion && x >= regiondata.Area.Left && x <= regiondata.Area.Right && y >= regiondata.Area.Top && y <= regiondata.Area.Bottom))
                            {
                                switch (Main.tile[x, y].type)
                                {
                                    case 117:
                                        Main.tile[x, y].type = 25;
                                        break;
                                    case 109:
                                        Main.tile[x, y].type = 23;
                                        break;
                                    case 116:
                                        Main.tile[x, y].type = 112;
                                        break;
                                    case 110:
                                        Main.tile[x, y].type = 24;
                                        break;
                                    case 113:
                                        Main.tile[x, y].type = 24;
                                        break;
                                    case 115:
                                        Main.tile[x, y].type = 52;
                                        break;
                                    default:
                                        continue;
                                }
                            }
                        }
                    }
                    WorldGen.CountTiles(0);
                    TSPlayer.All.SendData(PacketTypes.UpdateGoodEvil);
                    Netplay.ResetSections();
                    args.Player.SendMessage("Converted Hallow into Corruption!", Color.MediumSeaGreen);
                }
                else if (to == "normal")
                {
                    args.Player.SendMessage("Server might lag for a moment.", Color.IndianRed);
                    for (int x = 0; x < Main.maxTilesX; x++)
                    {
                        for (int y = 0; y < Main.maxTilesY; y++)
                        {
                            if (!doregion || (doregion && x >= regiondata.Area.Left && x <= regiondata.Area.Right && y >= regiondata.Area.Top && y <= regiondata.Area.Bottom))
                            {
                                switch (Main.tile[x, y].type)
                                {
                                    case 117:
                                        Main.tile[x, y].type = 1;
                                        break;
                                    case 109:
                                        Main.tile[x, y].type = 2;
                                        break;
                                    case 116:
                                        Main.tile[x, y].type = 53;
                                        break;
                                    case 110:
                                        Main.tile[x, y].type = 73;//Changed from 3 to 73
                                        break;
                                    case 113:
                                        Main.tile[x, y].type = 73;
                                        break;
                                    case 115:
                                        Main.tile[x, y].type = 52;
                                        break;
                                    default:
                                        continue;
                                }
                            }
                        }
                    }
                    WorldGen.CountTiles(0);
                    TSPlayer.All.SendData(PacketTypes.UpdateGoodEvil);
                    Netplay.ResetSections();
                    args.Player.SendMessage("Converted Hallow into Normal!", Color.MediumSeaGreen);
                }
            }
            else if (from == "corruption")
            {
                if (args.Parameters.Count == 3 && !doregion)
                    args.Player.SendMessage("You must specify a valid region to convert a normal biome.", Color.IndianRed);
                else if (to == "corruption")
                    args.Player.SendMessage("You cannot convert Corruption to Corruption.", Color.IndianRed);
                else if (to == "hallow")
                {
                    args.Player.SendMessage("Server might lag for a moment.", Color.IndianRed);
                    for (int x = 0; x < Main.maxTilesX; x++)
                    {
                        for (int y = 0; y < Main.maxTilesY; y++)
                        {
                            if (!doregion || (doregion && x >= regiondata.Area.Left && x <= regiondata.Area.Right && y >= regiondata.Area.Top && y <= regiondata.Area.Bottom))
                            {
                                switch (Main.tile[x, y].type)
                                {
                                    case 25:
                                        Main.tile[x, y].type = 117;
                                        break;
                                    case 23:
                                        Main.tile[x, y].type = 109;
                                        break;
                                    case 112:
                                        Main.tile[x, y].type = 116;
                                        break;
                                    case 24:
                                        Main.tile[x, y].type = 110;
                                        break;
                                    case 32:
                                        Main.tile[x, y].type = 115;
                                        break;
                                    default:
                                        continue;
                                }
                            }
                        }
                    }
                    WorldGen.CountTiles(0);
                    TSPlayer.All.SendData(PacketTypes.UpdateGoodEvil);
                    Netplay.ResetSections();
                    args.Player.SendMessage("Converted Corruption into Hallow!", Color.MediumSeaGreen);
                }
                else if (to == "normal")
                {
                    args.Player.SendMessage("Server might lag for a moment.", Color.IndianRed);
                    for (int x = 0; x < Main.maxTilesX; x++)
                    {
                        for (int y = 0; y < Main.maxTilesY; y++)
                        {
                            if (!doregion || (doregion && x >= regiondata.Area.Left && x <= regiondata.Area.Right && y >= regiondata.Area.Top && y <= regiondata.Area.Bottom))
                            {
                                switch (Main.tile[x, y].type)
                                {
                                    case 25:
                                        Main.tile[x, y].type = 1;
                                        break;
                                    case 23:
                                        Main.tile[x, y].type = 2;
                                        break;
                                    case 112:
                                        Main.tile[x, y].type = 53;
                                        break;
                                    case 24:
                                        Main.tile[x, y].type = 3;
                                        break;
                                    case 32:
                                        Main.tile[x, y].type = 52;
                                        break;
                                    default:
                                        continue;
                                }
                            }
                        }
                    }
                    WorldGen.CountTiles(0);
                    TSPlayer.All.SendData(PacketTypes.UpdateGoodEvil);
                    Netplay.ResetSections();
                    args.Player.SendMessage("Converted Corruption into Normal!", Color.MediumSeaGreen);
                }
                else
                {
                    args.Player.SendMessage("Error, Useable values: Hallow, Corruption, Normal", Color.IndianRed);
                }
            }
        }
        #endregion

        #region Seach IDs
        public static void spage(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendMessage("Usage: /spage <page>", Color.IndianRed);
                return;
            }

            if (args.Parameters.Count == 1)
            {
                int pge = 1;
                bool PageIsNum = int.TryParse(args.Parameters[0], out pge);
                if (!PageIsNum)
                {
                    args.Player.SendMessage("Specified page invalid!", Color.IndianRed);
                    return;
                }

                foreach (esPlayer play in esPlayers)
                {
                    if (play.plrName == args.Player.Name)
                    {
                        if (play.lastseachtype == "none")
                            args.Player.SendMessage("You must complete a Item/NPC id search first!", Color.IndianRed);
                        else if (play.lastseachtype == "Item")
                        {
                            var items = GetItemByName(play.lastsearch);
                            BCsearchitem(args, items, pge);
                        }
                        else if (play.lastseachtype == "NPC")
                        {
                            var npcs = GetNPCByName(play.lastsearch);
                            BCsearchnpc(args, npcs, pge);
                        }
                    }
                }
            }
        }

        public static void sitems(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendMessage("Usage: /sitem <search term>", Color.IndianRed);
                return;
            }

            if (args.Parameters.Count > 0)
            {
                string sterm = "";

                if (args.Parameters.Count == 1)
                    sterm = args.Parameters[0];
                else
                {
                    foreach (string wrd in args.Parameters)
                        sterm = sterm + wrd + " ";

                    sterm = sterm.Remove(sterm.Length - 1);
                }

                var items = GetItemByName(sterm);

                if (items.Count == 0)
                {
                    args.Player.SendMessage("Could not find any matching items!", Color.IndianRed);
                    return;
                }
                else
                {
                    foreach (esPlayer play in esPlayers)
                    {
                        if (play.plrName == args.Player.Name)
                        {
                            play.lastsearch = sterm;
                            play.lastseachtype = "Item";
                            BCsearchitem(args, items, 1);
                        }
                    }
                }
            }
        }

        public static void snpcs(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendMessage("Usage: /snpc <search term>", Color.IndianRed);
                return;
            }

            if (args.Parameters.Count > 0)
            {
                string sterm = "";

                if (args.Parameters.Count == 1)
                    sterm = args.Parameters[0];
                else
                {
                    foreach (string wrd in args.Parameters)
                        sterm = sterm + wrd + " ";

                    sterm = sterm.Remove(sterm.Length - 1);
                }

                var npcs = GetNPCByName(sterm);

                if (npcs.Count == 0)
                {
                    args.Player.SendMessage("Could not find any matching items!", Color.IndianRed);
                    return;
                }
                else
                {
                    foreach (esPlayer play in esPlayers)
                    {
                        if (play.plrName == args.Player.Name)
                        {
                            play.lastsearch = sterm;
                            play.lastseachtype = "NPC";
                            BCsearchnpc(args, npcs, 1);
                        }
                    }
                }
            }
        }
        #endregion

        #region MyHome
        public static void setmyhome(CommandArgs args)
        {
            if (args.Player.IsLoggedIn)
            {
                int homecount = 0;
                homecount = SQLEditor.ReadColumn("EssentialsUserHomes", "UserID", new List<SqlValue>()).Count;
                bool hashome = false;
                for (int i = 0; i < homecount; i++)
                {
                    int acname = Int32.Parse(SQLEditor.ReadColumn("EssentialsUserHomes", "UserID", new List<SqlValue>())[i].ToString());
                    int worldid = Int32.Parse(SQLEditor.ReadColumn("EssentialsUserHomes", "WorldID", new List<SqlValue>())[i].ToString());

                    if (acname == args.Player.UserID && worldid == Main.worldID)
                        hashome = true;
                }

                if (hashome)
                {
                    List<SqlValue> values = new List<SqlValue>();
                    values.Add(new SqlValue("HomeX", args.Player.TileX));
                    values.Add(new SqlValue("HomeY", args.Player.TileY));
                    List<SqlValue> where = new List<SqlValue>();
                    where.Add(new SqlValue("UserID", args.Player.UserID));
                    where.Add(new SqlValue("WorldID", Main.worldID));
                    SQLEditor.UpdateValues("EssentialsUserHomes", values, where);

                    args.Player.SendMessage("Updated your home position!", Color.MediumSeaGreen);
                }
                else
                {
                    List<SqlValue> list = new List<SqlValue>();
                    list.Add(new SqlValue("UserID", args.Player.UserID));
                    list.Add(new SqlValue("HomeX", args.Player.TileX));
                    list.Add(new SqlValue("HomeY", args.Player.TileY));
                    list.Add(new SqlValue("WorldID", Main.worldID));
                    SQLEditor.InsertValues("EssentialsUserHomes", list);

                    args.Player.SendMessage("Created your home!", Color.MediumSeaGreen);
                }
            }
            else
                args.Player.SendMessage("You must be logged in to do that!", Color.IndianRed);
        }

        public static void gomyhome(CommandArgs args)
        {
            if (args.Player.IsLoggedIn)
            {
                int homecount = 0;
                if ((homecount = SQLEditor.ReadColumn("EssentialsUserHomes", "UserID", new List<SqlValue>()).Count) != 0)
                {
                    bool hashome = false;
                    int homeid = 0;
                    for (int i = 0; i < homecount; i++)
                    {
                        int acname = Int32.Parse(SQLEditor.ReadColumn("EssentialsUserHomes", "UserID", new List<SqlValue>())[i].ToString());
                        int worldid = Int32.Parse(SQLEditor.ReadColumn("EssentialsUserHomes", "WorldID", new List<SqlValue>())[i].ToString());

                        if (acname == args.Player.UserID && worldid == Main.worldID)
                        {
                            hashome = true;
                            homeid = i;
                        }
                    }

                    if (hashome)
                    {
                        int homex = Int32.Parse(SQLEditor.ReadColumn("EssentialsUserHomes", "HomeX", new List<SqlValue>())[homeid].ToString());
                        int homey = Int32.Parse(SQLEditor.ReadColumn("EssentialsUserHomes", "HomeY", new List<SqlValue>())[homeid].ToString());

                        esPlayer play = GetesPlayerByName(args.Player.Name);
                        play.lastXtp = args.Player.TileX;
                        play.lastYtp = args.Player.TileY;
                        play.lastaction = "tp";

                        args.Player.Teleport(homex, homey);
                        args.Player.SendMessage("Teleported to your home!", Color.MediumSeaGreen);
                    }
                    else
                    {
                        args.Player.SendMessage("You have not set a home. type: \"/sethome\" to set one.", Color.IndianRed);
                    }
                }
                else
                {
                    args.Player.SendMessage("You have not set a home. type: \"/sethome\" to set one.", Color.IndianRed);
                }
            }
            else
                args.Player.SendMessage("You must be logged in to do that!", Color.IndianRed);
        }
        #endregion

        #region Essentials Reload
        public static void cmdessentials(CommandArgs args)
        {
            bool err = false;
            if (!File.Exists(@"tshock/EssentialsConfig.txt"))
            {
                File.WriteAllText(@"tshock/EssentialsConfig.txt", "#> < is a comment" + Environment.NewLine +
                    "#> To Completely disable locking of teams, set LockTeamsWithPermissions to false and set the passwords to nothing" + Environment.NewLine +
                    "#> Lock Teams with Permissions or passwords (Boolean):" + Environment.NewLine +
                    "LockTeamsWithPermissions:false" + Environment.NewLine +
                    "#> Passwords for teams (leave blank for no password) Make sure the passowrd is in quotes \"<password>\" (String):" + Environment.NewLine +
                    "RedPassword:\"\"" + Environment.NewLine +
                    "GreenPassword:\"\"" + Environment.NewLine +
                    "BluePassword:\"\"" + Environment.NewLine +
                    "YellowPassword:\"\"" + Environment.NewLine);
                useteamperms = false;
                redpassword = "";
                greenpassword = "";
                bluepassword = "";
                yellowpassword = "";
            }
            else
            {
                using (StreamReader file = new StreamReader(@"tshock/EssentialsConfig.txt", true))
                {
                    string[] rFile = (file.ReadToEnd()).Split('\n');
                    foreach (string currentLine in rFile)
                    {
                        try
                        {
                            if (currentLine.StartsWith("LockTeamsWithPermissions:"))
                            {
                                string tempLine = currentLine;
                                tempLine = tempLine.Remove(0, 25);
                                if (tempLine.StartsWith("false"))
                                    useteamperms = false;
                                else if (tempLine.StartsWith("true"))
                                    useteamperms = true;
                                else
                                {
                                    args.Player.SendMessage("Error in Essentials config file - LockTeamsWithPermissions", Color.IndianRed);
                                    err = true;
                                }
                            }
                            else if (currentLine.StartsWith("RedPassword:"))
                            {
                                string tempLine = currentLine;
                                tempLine = tempLine.Remove(0, 12);
                                tempLine = tempLine.Split('\"', '\'')[1];
                                redpassword = tempLine;
                            }
                            else if (currentLine.StartsWith("GreenPassword:"))
                            {
                                string tempLine = currentLine;
                                tempLine = tempLine.Remove(0, 14);
                                tempLine = tempLine.Split('\"', '\'')[1];
                                greenpassword = tempLine;
                            }
                            else if (currentLine.StartsWith("BluePassword:"))
                            {
                                string tempLine = currentLine;
                                tempLine = tempLine.Remove(0, 13);
                                tempLine = tempLine.Split('\"', '\'')[1];
                                bluepassword = tempLine;
                            }
                            else if (currentLine.StartsWith("YellowPassword:"))
                            {
                                string tempLine = currentLine;
                                tempLine = tempLine.Remove(0, 15);
                                tempLine = tempLine.Split('\"', '\'')[1];
                                yellowpassword = tempLine;
                            }
                        }
                        catch (Exception)
                        {
                            args.Player.SendMessage("Error in Essentials config file - TeamPasswords", Color.IndianRed);
                            err = true;
                            return;
                        }
                    }
                }
            }
            if (!err)
                args.Player.SendMessage("Config Reloaded Successfully!", Color.MediumSeaGreen);
        }
        #endregion

        #region jointeam
        public static void TeamUnlock(CommandArgs args)
        {
            if (useteamperms)
            {
                args.Player.SendMessage("You are not able to unlock teams!", Color.Red);
                return;
            }

            if (args.Parameters.Count < 2)
            {
                args.Player.SendMessage("Usage: /teamunlock <color> <password>", Color.Red);
                return;
            }

            string subcmd = args.Parameters[0].ToLower();
            string password = "";
            for (int i = 1; i < args.Parameters.Count; i++)
            {
                password = password + args.Parameters[i] + " ";
            }
            password = password.Remove(password.Length - 1, 1);

            esPlayer ply = GetesPlayerByID(args.Player.Index);

            if (subcmd == "red")
            {
                ply.redpass = password;
                if (password == redpassword)
                    args.Player.SendMessage("You can now join red team!", Color.MediumSeaGreen);
                else
                    args.Player.SendMessage("Incorrect Password!", Color.IndianRed);
            }
            else if (subcmd == "green")
            {
                ply.greenpass = password;
                if (password == greenpassword)
                    args.Player.SendMessage("You can now join green team!", Color.MediumSeaGreen);
                else
                    args.Player.SendMessage("Incorrect Password!", Color.IndianRed);
            }
            else if (subcmd == "blue")
            {
                ply.bluepass = password;
                if (password == bluepassword)
                    args.Player.SendMessage("You can now join blue team!", Color.MediumSeaGreen);
                else
                    args.Player.SendMessage("Incorrect Password!", Color.IndianRed);
            }
            else if (subcmd == "yellow")
            {
                ply.yellowpass = password;
                if (password == yellowpassword)
                    args.Player.SendMessage("You can now join yellow team!", Color.MediumSeaGreen);
                else
                    args.Player.SendMessage("Incorrect Password!", Color.IndianRed);
            }
            else
            {
                args.Player.SendMessage("Usage: /teamunlock <red/green/blue/yellow> <password>", Color.Red);
                return;
            }
        }
        #endregion
    }
    
    #region esPlayer
    public class esPlayer
    {

        public int Index { get; set; }
        public TSPlayer TSPlayer { get { return TShock.Players[Index]; } }
        public string plrName { get { return TShock.Players[Index].Name; } }
        public string plrGroup { get { return TShock.Players[Index].Group.Name; } }
        public Group grpData { get { return TShock.Players[Index].Group; } }
        public int lastXtp = 0;
        public int lastYtp = 0;
        public int lastXondeath = 0;
        public int lastYondeath = 0;
        public string lastaction = "none";
        public bool ondeath = false;
        public string lastsearch = "";
        public string lastseachtype = "none";
        public string redpass = "";
        public string greenpass = "";
        public string bluepass = "";
        public string yellowpass = "";

        public esPlayer(int index)
        {
            Index = index;
        }

        public void SendMessage(string message, Color color)
        {
            NetMessage.SendData((int)PacketTypes.ChatText, Index, -1, message, 255, color.R, color.G, color.B);
        }

        public void Kick(string reason)
        {
            TShock.Players[Index].Disconnect(reason);
        }

        public void Teleport(int xtile, int ytile)
        {
            TShock.Players[Index].Teleport(xtile, ytile);
        }
    }
    #endregion
}