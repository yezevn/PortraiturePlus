using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using Portraiture;
using Portraiture.HDP;
using StardewModdingAPI;
using StardewValley;
using System.Reflection;
namespace PortraiturePlus
{
	internal class PortraitFix
	{
		private static IMonitor Monitor = null!;
		
		internal static void Initialize(IMonitor monitor)
		{
			Monitor = monitor;
		}
		internal static MethodInfo TargetMethod()
		{
			return AccessTools.Method("TextureLoader:getPortrait", new[]
			{
				typeof(NPC), typeof(Texture2D)
			});
		}

		internal static bool getPortrait_Prefix(NPC npc, Texture2D tex, ref Texture2D? __result)
		{
			var felds = typeof(PortraitureMod).Assembly.GetType("Portraiture.PortraitureMod").GetRuntimeFields();
			foreach (var field in felds)
			{
				try
				{
					FileLog.Log(field.GetValue(null).ToString());
				}
				catch
				{
					// ignored
				}
			}
			return true;
		}
	}
}
