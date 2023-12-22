using BepInEx;
using BepInEx.Configuration;
using RiskOfOptions.Options;
using RiskOfOptions;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.IO;

namespace ItemEveryStage
{
    [BepInDependency("com.rune580.riskofoptions")]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class ItemEveryStagePlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "Lawlzee.ItemEveryStage";
        public const string PluginAuthor = "Lawlzee";
        public const string PluginName = "ItemEveryStage";
        public const string PluginVersion = "1.0.0";

        private const string _itemsToGiveDescription = """
            Controls which items are given at every stage.

            Format:
            <count>:<item>,<count>:<item>...

            Examples:
            2:hoof
            1:clover,3:firework

            All the items are:

            """;

        private ConfigEntry<bool> _modEnabled;
        private ConfigEntry<string> _itemsToGive;
        private StringInputFieldOption _itemsToGiveOption;

        private int _oldStageCount;

        public void Awake()
        {
            Log.Init(Logger);

            _modEnabled = Config.Bind("Configuration", "Mod enabled", true, "Mod enabled");
            ModSettingsManager.AddOption(new CheckBoxOption(_modEnabled));

            _itemsToGive = Config.Bind("Configuration", "Items given at every stage", "1:Hoof", _itemsToGiveDescription);
            _itemsToGiveOption = new StringInputFieldOption(_itemsToGive);
            ModSettingsManager.AddOption(_itemsToGiveOption);

            var texture = LoadTexture("icon.png");
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0, 0));
            ModSettingsManager.SetModIcon(sprite);

            Run.onRunStartGlobal += Run_onRunStartGlobal;
            Stage.onServerStageBegin += Stage_onServerStageBegin;
            On.RoR2.ItemCatalog.Init += ItemCatalog_Init;
        }

        private void ItemCatalog_Init(On.RoR2.ItemCatalog.orig_Init orig)
        {
            orig();

            if (!_modEnabled.Value)
            {
                return;
            }

            var orderedItemNames = ItemCatalog.itemDefs
                .Select(x => $"{Language.GetString(x.nameToken)}: '{x.name}'")
                .OrderBy(x => x);

            string listOfitems = string.Join(Environment.NewLine, orderedItemNames);

            _itemsToGiveOption.SetDescription($"{_itemsToGiveDescription}{listOfitems}", new());
        }

        private Texture2D LoadTexture(string name)
        {
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(File.ReadAllBytes(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Info.Location), name)));
            return texture;
        }

        private void Run_onRunStartGlobal(Run obj)
        {
            if (!_modEnabled.Value)
            {
                return;
            }

            _oldStageCount = -1;
            Log.Debug(nameof(Run_onRunStartGlobal));
        }

        private void Stage_onServerStageBegin(Stage obj)
        {
            if (!_modEnabled.Value)
            {
                return;
            }

            Log.Debug(nameof(Stage_onServerStageBegin));

            int stageCleared = Run.instance.stageClearCount;
            if (_oldStageCount != stageCleared)
            {
                foreach (var character in CharacterMaster.readOnlyInstancesList)
                {
                    foreach (var itemGift in GetItemGifts())
                    {
                        character.inventory.GiveItem(itemGift.ItemIndex, itemGift.Quantity);
                        GenericPickupController.SendPickupMessage(character, PickupCatalog.FindPickupIndex(itemGift.ItemIndex));
                    }
                }
            }

            _oldStageCount = stageCleared;
        }

        private class ItemGift
        {
            public ItemIndex ItemIndex { get; }
            public int Quantity { get; }

            public ItemGift(ItemIndex itemIndex, int quantity)
            {
                ItemIndex = itemIndex;
                Quantity = quantity;
            }
        }

        private IEnumerable<ItemGift> GetItemGifts()
        {
            string[] configs = _itemsToGive.Value.Split([','], StringSplitOptions.RemoveEmptyEntries);
            foreach (var config in configs)
            {
                string[] configParts = config.Split([':'], StringSplitOptions.RemoveEmptyEntries);
                if (configParts.Length == 2) 
                {
                    string quantity = configParts[0].Trim();
                    string item = configParts[1].Trim();

                    if (int.TryParse(quantity, out int quantityValue))
                    {
                        foreach (var itemDef in ItemCatalog.itemDefs)
                        {
                            if (item.Equals(itemDef.name, StringComparison.OrdinalIgnoreCase))
                            {
                                yield return new ItemGift(itemDef.itemIndex, quantityValue);
                            }
                        }
                    }
                }
            }
        }
    }
}
