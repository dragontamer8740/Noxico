using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;

namespace Noxico
{
	public class Introduction
	{
		public static void Title()
		{
			NoxicoGame.Mode = UserMode.Subscreen;
			NoxicoGame.Subscreen = Introduction.TitleHandler;
			NoxicoGame.Immediate = true;
		}

		public static void TitleHandler()
		{
			var host = NoxicoGame.HostForm;
			if (Subscreens.FirstDraw)
			{
				Subscreens.FirstDraw = false;
				host.Clear();
				new UIPNGBackground(Mix.GetBitmap("title.png")).Draw();

				var subtitle = i18n.GetString("ts_subtitle");
				var pressEnter = "\xC4\xC4\xC4\xC4\xB4 " + i18n.GetString("ts_pressentertobegin") + " <cGray>\xC4\xC4\xC4\xC4\xC4";
				host.Write(subtitle, Color.Teal, Color.Transparent, 20, 25 - subtitle.Length() / 2);
				host.Write(pressEnter, Color.Gray, Color.Transparent, 22, 25 - pressEnter.Length() / 2);
			}
			if (NoxicoGame.IsKeyDown(KeyBinding.Accept) || Subscreens.Mouse || Vista.Triggers != 0)
			{
				if (Subscreens.Mouse)
					Subscreens.UsingMouse = true;
				Subscreens.Mouse = false;
				Subscreens.FirstDraw = true;
				var rawSaves = Directory.GetDirectories(NoxicoGame.SavePath);
				var saves = new List<string>();
				foreach (var s in rawSaves)
				{
					var verCheck = Path.Combine(s, "version");
					if (!File.Exists(verCheck))
						continue;
					var version = int.Parse(File.ReadAllText(verCheck));
					if (version < 20)
						continue;
					if (File.Exists(Path.Combine(s, "global.bin")))
						saves.Add(s);
				}
				NoxicoGame.ClearKeys();
				Subscreens.Mouse = false;
				var options = saves.ToDictionary(new Func<string, object>(s => Path.GetFileName(s)), new Func<string, string>(s =>
				{
					string p;
					var playerFile = Path.Combine(s, "player.bin");
					if (File.Exists(playerFile))
					{
						using (var f = new BinaryReader(File.OpenRead(playerFile)))
						{
							//p = f.ReadString();
							p = Player.LoadFromFile(f).Character.Name.ToString(true);
						}
						return i18n.Format("ts_loadgame", p, Path.GetFileName(s));
					}
					return i18n.Format("ts_startoverinx", Path.GetFileName(s));
				}));
				options.Add("~", i18n.GetString("ts_startnewgame"));
				MessageBox.List(saves.Count == 0 ? i18n.GetString("ts_welcometonoxico") : i18n.GetString(saves.Count == 1 ? "ts_thereisasave" : "ts_therearesaves"), options,
					() =>
					{
						if ((string)MessageBox.Answer == "~")
						{
							NoxicoGame.Mode = UserMode.Subscreen;
							NoxicoGame.Subscreen = Introduction.CharacterCreator;
							NoxicoGame.Immediate = true;
						}
						else
						{
							NoxicoGame.WorldName = (string)MessageBox.Answer;
							host.Noxico.LoadGame();
							NoxicoGame.HostForm.Noxico.CurrentBoard.Draw();
							Subscreens.FirstDraw = true;
							NoxicoGame.Immediate = true;
							NoxicoGame.AddMessage(i18n.GetString("welcomeback"), Color.Yellow);
							NoxicoGame.AddMessage(i18n.GetString("rememberhelp"));
							//TextScroller.LookAt(NoxicoGame.HostForm.Noxico.Player);
							NoxicoGame.Mode = UserMode.Walkabout;
						}
					}
				);
			}
		}


		private class PlayableRace
		{
			public string ID { get; set; }
			public string Name { get; set; }
			public string Skin { get; set; }
			public List<string> HairColors { get; set; }
			public List<string> SkinColors { get; set; }
			public List<string> EyeColors { get; set; }
			public bool[] SexLocks { get; set; }
			public string Bestiary { get; set; }
			public override string ToString()
			{
				return Name;
			}
		}

		private static List<PlayableRace> CollectPlayables()
		{
			var ret = new List<PlayableRace>();
			Program.WriteLine("Collecting playables...");
			TokenCarrier.NoRolls = true; //Bit of a hack, I know. It resets to false when Tokenize() is finished.
			var plans = Mix.GetTokenTree("bodyplans.tml");
			foreach (var bodyPlan in plans.Where(t => t.Name == "bodyplan"))
			{
				var id = bodyPlan.Text;
				var plan = bodyPlan.Tokens;
				if (!bodyPlan.HasToken("playable"))
					continue;
				Program.WriteLine(" * Parsing {0}...", id);

				var sexlocks = new[] { true, true, true, false };
				if (bodyPlan.HasToken("normalgenders"))
					sexlocks = new[] { true, true, false, false };
				else if (bodyPlan.HasToken("maleonly"))
					sexlocks = new[] { true, false, false, false };
				else if (bodyPlan.HasToken("femaleonly"))
					sexlocks = new[] { false, true, false, false };
				else if (bodyPlan.HasToken("hermonly"))
					sexlocks = new[] { false, false, true, false };
				else if (bodyPlan.HasToken("neuteronly"))
					sexlocks = new[] { false, false, false, true };
				if (bodyPlan.HasToken("allowneuter"))
					sexlocks[3] = true;

				var name = id.Replace('_', ' ').Titlecase();
				if (!string.IsNullOrWhiteSpace(bodyPlan.GetToken("playable").Text))
					name = bodyPlan.GetToken("playable").Text;

				var bestiary = bodyPlan.HasToken("bestiary") ? bodyPlan.GetToken("bestiary").Text : string.Empty;				

				var hairs = new List<string>() { "<None>" };
				var hair = bodyPlan.GetToken("hair");
				if (hair != null)
				{
					var c = hair.GetToken("color").Text;
					if (c.StartsWith("oneof"))
					{
						hairs.Clear();
						c = c.Substring(6);
						var oneof = c.Split(',').ToList();
						oneof.ForEach(x => hairs.Add(Color.NameColor(x.Trim()).Titlecase()));
					}
					else
					{
						hairs[0] = c.Titlecase();
					}
				}

				var eyes = new List<string>() { "Brown" };
				{
					var c = bodyPlan.GetToken("eyes").Text;
					if (c.StartsWith("oneof"))
					{
						eyes.Clear();
						c = c.Substring(6);
						var oneof = c.Split(',').ToList();
						oneof.ForEach(x => eyes.Add(Color.NameColor(x.Trim()).Titlecase()));
					}
					else
					{
						eyes[0] = c.Titlecase();
					}
				}

				var skins = new List<string>();
				var skinName = "skin";
				var s = bodyPlan.GetToken("skin");
				if (s != null)
				{
					if (s.HasToken("type"))
					{
						skinName = s.GetToken("type").Text;
					}
					var c = s.GetToken("color").Text;
					if (c.StartsWith("oneof"))
					{
						skins.Clear();
						c = c.Substring(6);
						var oneof = c.Split(',').ToList();
						oneof.ForEach(x => skins.Add(Color.NameColor(x.Trim()).Titlecase()));
					}
					else
					{
						skins.Add(Color.NameColor(c).Titlecase());
					}
				}

				if (skins.Count > 0)
					skins = skins.Distinct().ToList();
				skins.Sort();

				ret.Add(new PlayableRace() { ID = id, Name = name, Bestiary = bestiary, HairColors = hairs, SkinColors = skins, Skin = skinName, EyeColors = eyes, SexLocks = sexlocks });

			}
			return ret;
		}
		private static List<PlayableRace> playables;

		private static Dictionary<string, UIElement> controls;
		private static List<UIElement>[] pages;
		private static Dictionary<string, string> controlHelps;

		private static int page = 0;
		private static Action<int> loadPage, loadColors;

		public static void CharacterCreator()
		{
			if (Subscreens.FirstDraw)
			{
				var traits = new List<string>();
				var traitHelps = new List<string>();
				var traitsDoc = Mix.GetTokenTree("bonustraits.tml");
				foreach (var trait in traitsDoc.Where(t => t.Name == "trait"))
				{
					traits.Add(trait.GetToken("display").Text);
					traitHelps.Add(trait.GetToken("description").Text);
				}
				controlHelps = new Dictionary<string, string>()
				{
					{ "back", i18n.GetString("cchelp_back") },
					{ "next", i18n.GetString("cchelp_next") },
					{ "play", Random.NextDouble() > 0.7 ? "FRUITY ANGELS MOLEST SHARKY" : "ENGAGE RIDLEY MOTHER FUCKER" },
					{ "world", i18n.GetString("cchelp_world") },
					{ "name", i18n.GetString("cchelp_name") },
					{ "species", string.Empty },
					{ "sex", i18n.GetString("cchelp_sex") },
					{ "gid", i18n.GetString("cchelp_gid") },
					{ "pref", i18n.GetString("cchelp_pref") },
					{ "easy", i18n.GetString("cchelp_easy") },
					{ "hair", i18n.GetString("cchelp_hair") },
					{ "body", i18n.GetString("cchelp_body") },
					{ "eyes", i18n.GetString("cchelp_eyes") },
					{ "gift", traitHelps[0] },
				};

				var title = "\xB4 " + i18n.GetString("cc_title") + " \xC3";
				var bar = new string('\xC4', 33);
				string[] sexoptions = {i18n.GetString("Male"), i18n.GetString("Female"), i18n.GetString("Herm"), i18n.GetString("Neuter")};
				string[] prefoptions = { i18n.GetString("Male"), i18n.GetString("Female"), i18n.GetString("Either") };
				controls = new Dictionary<string, UIElement>()
				{
					{ "backdrop", new UIPNGBackground(Mix.GetBitmap("chargen.png")) },
					{ "headerline", new UILabel(bar) { Left = 56, Top = 8, Foreground = Color.Black } },
					{ "header", new UILabel(title) { Left = 73 - (title.Length() / 2), Top = 8, Width = title.Length(), Foreground = Color.Black } },
					{ "back", new UIButton(i18n.GetString("cc_back"), null) { Left = 58, Top = 46, Width = 10, Height = 3 } },
					{ "next", new UIButton(i18n.GetString("cc_next"), null) { Left = 78, Top = 46, Width = 10, Height = 3 } },
					{ "play", new UIButton(i18n.GetString("cc_play"), null) { Left = 78, Top = 46, Width = 10, Height = 3 } },

					{ "worldLabel", new UILabel(i18n.GetString("cc_world")) { Left = 56, Top = 10, Foreground = Color.Gray } },
					{ "world", new UITextBox(NoxicoGame.RollWorldName()) { Left = 58, Top = 11, Width = 24, Foreground = Color.Black, Background = Color.Transparent } },
					{ "nameLabel", new UILabel(i18n.GetString("cc_name")) { Left = 56, Top = 14, Foreground = Color.Gray } },
					{ "name", new UITextBox(string.Empty) { Left = 58, Top = 15, Width = 24, Foreground = Color.Black, Background = Color.Transparent } },
					{ "nameRandom", new UILabel(i18n.GetString("cc_random")) { Left = 60, Top = 15, Foreground = Color.Gray } },
					{ "speciesLabel", new UILabel(i18n.GetString("cc_species")) { Left = 56, Top = 18, Foreground = Color.Gray } },
					{ "species", new UISingleList() { Left = 58, Top = 19, Width = 30, Foreground = Color.Black, Background = Color.Transparent } },
					{ "sexLabel", new UILabel(i18n.GetString("cc_sex")) { Left = 56, Top = 22, Foreground = Color.Gray } },
					{ "sex", new UIRadioList(sexoptions) { Left = 58, Top = 23, Width = 24, Foreground = Color.Black, Background = Color.Transparent } },
					{ "gidLabel", new UILabel(i18n.GetString("cc_gid")) { Left = 56, Top = 29, Foreground = Color.Gray } },
					{ "gid", new UIRadioList(sexoptions) { Left = 58, Top = 30, Width = 24, Foreground = Color.Black, Background = Color.Transparent } },
					{ "prefLabel", new UILabel(i18n.GetString("cc_pref")) { Left = 56, Top = 36, Foreground = Color.Gray } },
					{ "pref", new UIRadioList(prefoptions) { Left = 58, Top = 37, Width = 24, Foreground = Color.Black, Background = Color.Transparent } },
					{ "easy", new UIToggle(i18n.GetString("cc_easy")) { Left = 58, Top = 42, Width = 24, Foreground = Color.Black, Background = Color.Transparent } },

					{ "hairLabel", new UILabel(i18n.GetString("cc_hair")) { Left = 56, Top = 10, Foreground = Color.Gray } },
					{ "hair", new UIColorList() { Left = 58, Top = 11, Width = 30, Foreground = Color.Black, Background = Color.Transparent } },
					{ "bodyLabel", new UILabel(i18n.GetString("cc_body")) { Left = 56, Top = 14, Foreground = Color.Gray } },
					{ "bodyNo", new UILabel(i18n.GetString("cc_no")) { Left = 60, Top = 15, Foreground = Color.Gray } },
					{ "body", new UIColorList() { Left = 58, Top = 15, Width = 30, Foreground = Color.Black, Background = Color.Transparent } },
					{ "eyesLabel", new UILabel(i18n.GetString("cc_eyes")) { Left = 56, Top = 18, Foreground = Color.Gray } },
					{ "eyes", new UIColorList() { Left = 58, Top = 19, Width = 30, Foreground = Color.Black, Background = Color.Transparent } },

					{ "giftLabel", new UILabel(i18n.GetString("cc_gift")) { Left = 56, Top = 10, Foreground = Color.Gray } },
					{ "gift", new UIList("", null, traits) { Left = 58, Top = 12, Width = 30, Height = 32, Foreground = Color.Black, Background = Color.Transparent } },

					{ "controlHelp", new UILabel(traitHelps[0]) { Left = 1, Top = 8, Width = 50, Height = 4, Foreground = Color.White } },
					{ "topHeader", new UILabel(i18n.GetString("cc_header")) { Left = 1, Top = 0, Foreground = Color.Silver } },
					{ "helpLine", new UILabel(i18n.GetString("cc_footer")) { Left = 1, Top = 59, Foreground = Color.Silver } },
				};

				pages = new List<UIElement>[]
				{
					new List<UIElement>()
					{
						controls["backdrop"], controls["headerline"], controls["header"], controls["topHeader"], controls["helpLine"],
						controls["worldLabel"], controls["world"],
						controls["nameLabel"], controls["name"], controls["nameRandom"],
						controls["speciesLabel"], controls["species"],
						controls["sexLabel"], controls["sex"], controls["gidLabel"], controls["gid"], controls["prefLabel"], controls["pref"],
						controls["easy"],
						controls["controlHelp"], controls["next"],
					},
					new List<UIElement>()
					{
						controls["backdrop"], controls["headerline"], controls["header"], controls["topHeader"], controls["helpLine"],
						controls["hairLabel"], controls["hair"],
						controls["bodyLabel"], controls["bodyNo"], controls["body"],
						controls["eyesLabel"], controls["eyes"],
						controls["controlHelp"], controls["back"], controls["next"],
					},
					new List<UIElement>()
					{
						controls["backdrop"], controls["headerline"], controls["header"], controls["topHeader"], controls["helpLine"],
						controls["giftLabel"], controls["gift"],
						controls["controlHelp"], controls["back"], /* controls["playNo"], */ controls["play"],
					},
				};

				playables = CollectPlayables();

				loadPage = new Action<int>(p =>
				{
					UIManager.Elements.Clear();
					UIManager.Elements.AddRange(pages[page]);
					UIManager.Highlight = UIManager.Elements[5];
				});

				loadColors = new Action<int>(i =>
				{
					var species = playables[i];
					controlHelps["species"] = species.Bestiary; 
					controls["bodyLabel"].Text = species.Skin.Titlecase();
					((UISingleList)controls["hair"]).Items.Clear();
					((UISingleList)controls["body"]).Items.Clear();
					((UISingleList)controls["eyes"]).Items.Clear();
					((UISingleList)controls["hair"]).Items.AddRange(species.HairColors);
					((UISingleList)controls["body"]).Items.AddRange(species.SkinColors);
					((UISingleList)controls["eyes"]).Items.AddRange(species.EyeColors);
					((UISingleList)controls["hair"]).Index = 0;
					((UISingleList)controls["body"]).Index = 0;
					((UISingleList)controls["eyes"]).Index = 0;
				});

				controls["back"].Enter = (s, e) => { page--; loadPage(page); UIManager.Draw(); };
				controls["next"].Enter = (s, e) => { page++; loadPage(page); UIManager.Draw(); };
				controls["play"].Enter = (s, e) =>
				{
					NoxicoGame.WorldName = controls["world"].Text.Replace(':', '_').Replace('\\', '_').Replace('/', '_').Replace('"', '_');
					var playerName = controls["name"].Text;
					var sex = ((UIRadioList)controls["sex"]).Value;
					var gid = ((UIRadioList)controls["gid"]).Value;
					var pref = ((UIRadioList)controls["pref"]).Value;
					var species = ((UISingleList)controls["species"]).Index;
					var easy = ((UIToggle)controls["easy"]).Checked;
					var hair = ((UISingleList)controls["hair"]).Text;
					var body = ((UISingleList)controls["body"]).Text;
					var eyes = ((UISingleList)controls["eyes"]).Text;
					var bonus = ((UIList)controls["gift"]).Text;
					NoxicoGame.HostForm.Noxico.CreatePlayerCharacter(playerName.Trim(), (Gender)(sex + 1), (Gender)(gid + 1), pref, playables[species].ID, hair, body, eyes, bonus);
					if (easy)
						NoxicoGame.HostForm.Noxico.Player.Character.AddToken("easymode");
					NoxicoGame.HostForm.Noxico.CreateRealm();
					NoxicoGame.InGameTime.AddYears(Random.Next(0, 10));
					NoxicoGame.InGameTime.AddDays(Random.Next(20, 340));
					NoxicoGame.InGameTime.AddHours(Random.Next(10, 54));
					NoxicoGame.HostForm.Noxico.CurrentBoard.UpdateLightmap(NoxicoGame.HostForm.Noxico.Player, true);
					NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
					NoxicoGame.HostForm.Noxico.CurrentBoard.Draw();
					Subscreens.FirstDraw = true;
					NoxicoGame.Immediate = true;

					NoxicoGame.AddMessage(i18n.GetString("welcometonoxico"), Color.Yellow);
					NoxicoGame.AddMessage(i18n.GetString("rememberhelp"));
					TextScroller.LookAt(NoxicoGame.HostForm.Noxico.Player);

					if (!IniFile.GetValue("misc", "skipintro", true))
					{
						var dream = new Character();
						dream.Name = new Name("Dream");
						dream.AddToken("special");
						NoxicoGame.HostForm.Noxico.Player.Character.AddToken("introdream");
						SceneSystem.Engage(NoxicoGame.HostForm.Noxico.Player.Character, dream, "(new game start)");
					}
				};

				((UISingleList)controls["species"]).Items.Clear();
				playables.ForEach(x => ((UISingleList)controls["species"]).Items.Add(x.Name.Titlecase()));
				((UISingleList)controls["species"]).Index = 0;
				loadColors(0);
				((UIRadioList)controls["sex"]).ItemsEnabled = playables[0].SexLocks;
				controls["species"].Change = (s, e) =>
				{
					var speciesIndex = ((UISingleList)controls["species"]).Index;
					loadColors(speciesIndex);
					var playable = playables[speciesIndex];
					controlHelps["species"] = playable.Bestiary;
					controls["controlHelp"].Text = playable.Bestiary.Wordwrap(controls["controlHelp"].Width);
					var sexList = (UIRadioList)controls["sex"];
					//controls["sex"].Hidden = playable.GenderLocked;
					//controls["sexNo"].Hidden = !playable.GenderLocked;
					sexList.ItemsEnabled = playable.SexLocks;
					if (!sexList.ItemsEnabled[sexList.Value])
					{
						for (var i = 0; i < sexList.ItemsEnabled.Length; i++)
						{
							if (sexList.ItemsEnabled[i])
							{
								sexList.Value = i;
								break;
							}
					}
					}
					UIManager.Draw();
				};
				controls["world"].Change = (s, e) =>
				{
					controls["next"].Hidden = string.IsNullOrWhiteSpace(controls["world"].Text);
					UIManager.Draw();
				};
				controls["name"].Change = (s, e) =>
				{
					controls["nameRandom"].Hidden = !string.IsNullOrWhiteSpace(controls["name"].Text);
					UIManager.Draw();
				};
				controls["gift"].Change = (s, e) =>
				{
					var giftIndex = ((UIList)controls["gift"]).Index;
					controls["controlHelp"].Text = traitHelps[giftIndex].Wordwrap(50);
					controls["controlHelp"].Top = controls["gift"].Top + giftIndex;
					UIManager.Draw();
				};

				UIManager.Initialize();
				UIManager.HighlightChanged = (s, e) =>
				{
					var c = controls.First(x => x.Value == UIManager.Highlight);
					if (controlHelps.ContainsKey(c.Key))
					{
						controls["controlHelp"].Text = controlHelps[c.Key].Wordwrap(controls["controlHelp"].Width);
						controls["controlHelp"].Top = c.Value.Top;
					}
					else
						controls["controlHelp"].Text = "";
					UIManager.Draw();
				};
				loadPage(page);
				Subscreens.FirstDraw = false;
				Subscreens.Redraw = true;
				UIManager.HighlightChanged(null, null);

				NoxicoGame.InGame = false;
			}

			if (Subscreens.Redraw)
			{
				UIManager.Draw();
				Subscreens.Redraw = false;
			}

			UIManager.CheckKeys();
		}
	}

}
