using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using Portraiture;
using Portraiture.HDP;
using StardewModdingAPI;
using StardewValley;

namespace PortraiturePlus
{
	/// <summary>The mod entry point.</summary>
	// ReSharper disable once ClassNeverInstantiated.Global
	internal sealed class PortraiturePlusMod : Mod
	{
		public static IModHelper helper = null!;
		private static readonly string week = (Game1.dayOfMonth % 7) switch
		{
			0 => "Sunday",
			1 => "Monday",
			2 => "Tuesday",
			3 => "Wednesday",
			4 => "Thursday",
			5 => "Friday",
			6 => "Saturday",
			_ => ""
		};
		
		private static readonly IDictionary<string, string> festivalDates = Game1.content.Load<Dictionary<string, string>>(@"Data\Festivals\FestivalDates", LocalizedContentManager.LanguageCode.en);
		/// <summary>The mod entry point, called after the mod is first loaded.</summary>
		/// <param name="help">Provides simplified APIs for writing mods.</param>
		public override void Entry(IModHelper? help)
		{
			helper = help!;
			//var folders = new List<string>();
			//var presets = new Dictionary<string, Texture2D>();
			//addContentPackTextures(folders, presets);
			festivalInit();
			harmonyFix();
		}
		
		public static void addContentPackTextures(List<string> folders, Dictionary<string, Texture2D> pTextures)
		{
			var contentPacks = helper.ContentPacks.GetOwned();
			foreach (var pack in contentPacks)
			{
				var folderName = pack.Manifest.UniqueID;
				var folderPath = pack.DirectoryPath;

				folders.Add(folderName);
				foreach (var file in Directory.EnumerateFiles(pack.DirectoryPath, "*.*", SearchOption.AllDirectories).Where(s => s.EndsWith(".png") || s.EndsWith(".xnb")))
				{
					var fileName = file.Replace(folderPath + "\\", "");
					var name = Path.GetFileNameWithoutExtension(file);
					var extension = Path.GetExtension(file).ToLower();
					if (extension == "xnb")
						fileName = name;
					var texture = pack.ModContent.Load<Texture2D>(fileName);
					var tileWith = Math.Max(texture.Width / 2, 64);
					var scale = tileWith / 64f;
					var scaled = new ScaledTexture2D(texture, scale);
					if (!pTextures.ContainsKey(folderName + ">" + name))
						pTextures.Add(folderName + ">" + name, scaled);
					else
						pTextures[folderName + ">" + name] = scaled;
				}
			}
		}

		private void harmonyFix()
		{
			PortraiturePlusFix.Initialize(monitor: Monitor);
			var harmony = new Harmony(ModManifest.UniqueID);
			harmony.PatchAll();
			harmony.Patch(original: PortraiturePlusFix.getPortrait(), prefix: new HarmonyMethod(AccessTools.Method(typeof(PortraiturePlusFix), nameof(PortraiturePlusFix.getPortrait_Prefix))));
			harmony.Patch(original: PortraiturePlusFix.loadAllPortraits(), postfix: new HarmonyMethod(AccessTools.Method(typeof(PortraiturePlusFix), nameof(PortraiturePlusFix.loadAllPortraits_Postfix))));
		}
		
		public static Texture2D? getPortrait(NPC npc, Texture2D tex, List<string> folders, PresetCollection presets, int activeFolder, Dictionary<string, Texture2D> pTextures)
		{
			var name = npc.Name;

			if (!Context.IsWorldReady || folders.Count == 0)
				return null;

			activeFolder = Math.Max(activeFolder, 0);

			if (presets.Presets.FirstOrDefault(pr => pr.Character == name) is { } pre)
				activeFolder = Math.Max(folders.IndexOf(pre.Portraits), 0);

			var folder = folders[activeFolder];

			if (activeFolder == 0 || folders.Count <= activeFolder || folder == "none" || folder == "HDP" && PortraitureMod.helper.ModRegistry.IsLoaded("tlitookilakin.HDPortraits"))
				return null;

			if (folder == "HDP" && !PortraitureMod.helper.ModRegistry.IsLoaded("tlitookilakin.HDPortraits"))
			{
				try
				{
					var portraits = PortraitureMod.helper.GameContent.Load<MetadataModel>("Mods/HDPortraits/" + name);
					switch (portraits)
					{
						case null:
							return null;
						case var _ when portraits.TryGetTexture(out var texture):
							{
								if (portraits.Animation == null || portraits.Animation.VFrames == 1 && portraits.Animation.HFrames == 1)
									return ScaledTexture2D.FromTexture(tex, texture, portraits.Size / 64f);
								portraits.Animation.Reset();
								return new AnimatedTexture2D(texture, texture.Width / portraits.Animation.VFrames, texture.Height / portraits.Animation.HFrames, 6, true, portraits.Size / 64f);
							}
						default:
							return null;
					}
				}
				catch
				{
					return null;
				}
			}
			
			var season = Game1.currentSeason ?? "spring";
			var npcDictionary = pTextures.Keys
				.Where(key => key.Contains(name) && key.Contains(folder))
				.ToDictionary(k => k.ToLowerInvariant(), l => pTextures[l]);
			var dayOfMonth = Game1.dayOfMonth.ToString();
			var festival = GetDayEvent();
			var gl = Game1.currentLocation.Name ?? "";
			var isOutdoors = Game1.currentLocation.IsOutdoors ? "Outdoor" : "Indoor";
			// var isRaining = Game1.isRaining ? "_Rain" : "";
			name = folder + ">" + name;

			var queryScenarios = new List<string[]>
			{
				new[] {name, festival},
				new[] {name, gl, season, dayOfMonth}, new[] {name, gl, season, week},
				new[] {name, gl, season},
				new[] {name, gl, dayOfMonth}, new[] {name, gl, week},
				new[] {name, gl},
				new[] {name, season, isOutdoors},
				new[] {name, season, dayOfMonth}, new[] {name, season, week},
				new[] {name, season},
				new[] {name}
			};

			foreach (var result in queryScenarios.Select(args => getTexture2D(npcDictionary, args)).OfType<Texture2D>())
			{
				return result;
			}

			return pTextures.ContainsKey(folder + ">" + name) ? pTextures[folder + ">" + name] : null;
		}
		
		private static string GetDayEvent()
		{
			if (SaveGame.loaded?.weddingToday ?? Game1.weddingToday || Game1.CurrentEvent != null && Game1.CurrentEvent.isWedding)
				return "Wedding";

			var festival = festivalDates.TryGetValue($"{Game1.currentSeason}{Game1.dayOfMonth}", out var festivalName) ? festivalName : "";
			return festival;
		}
		
		private static Texture2D? getTexture2D(Dictionary<string, Texture2D> npcDictionary, params string[] values)
		{
			var key = values.Aggregate((current, next) => current + "_" + next).ToLowerInvariant().TrimEnd('_');
			return values.Any(text => text == "") ? null : npcDictionary!.GetValueOrDefault(key, null);
		}

		private static void festivalInit()
		{
			foreach (var key in festivalDates.Keys)
			{
				if (festivalDates[key].Contains(' '))
				{
					festivalDates[key] = festivalDates[key].Replace(" ", "");
				}
				if (festivalDates[key].Contains('\''))
				{
					festivalDates[key] = festivalDates[key].Replace("'", "");
				}
				festivalDates[key] = festivalDates[key] switch
				{
					"EggFestival" => "EggF",
					"DanceoftheMoonlightJellies" => "Jellies",
					"StardewValleyFair" => "Fair",
					"FestivalofIce" => "Ice",
					"FeastoftheWinterStar" => "WinterStar",
					_ => festivalDates[key]
				};
			}
		}
	}
}
