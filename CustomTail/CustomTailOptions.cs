using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OptionalUI;
using Partiality.Modloader;
using UnityEngine;

namespace CustomTail
{
    public partial class CustomTail
    {
        public static TailConfig[] configs = new TailConfig[4];

        public static TailConfig GetTailConfig(int playerNumber)
        {
            if (playerNumber >= 0 && playerNumber < configs.Length)
                return configs[playerNumber] ?? new TailConfig();
            else
                return configs[0] ?? new TailConfig();
        }

        public OptionInterface LoadOI()
        {
            return new CustomTailOptions(this);
        }
    }

    public class TailConfig
    {
        public string sprite;
        public Color baseTint;
        public Color tipTint;

        public TailConfig()
        {
            sprite = "Futile_White";
            baseTint = Color.black;
            tipTint = Color.black;
        }
    }

    public class CustomTailOptions : OptionInterface
    {
        private OpTextBox[] _sprites;

        public CustomTailOptions(PartialityMod mod) : base(mod: mod)
        {}

        public override void Initialize()
        {
            base.Initialize();
            Tabs = new OpTab[CustomTail.configs.Length];
            _sprites = new OpTextBox[CustomTail.configs.Length];
            for (int i = 0; i < CustomTail.configs.Length; i++)
            {
                Tabs[i] = new OpTab("Player " + (i + 1));
                InitTab(Tabs[i], i);
            }
        }

        private void InitTab(OpTab tab, int ply)
        {
            TailConfig defaults = new TailConfig();

            const string colorDesc = "Set to fully black to use the player's color instead.";
            const string baseTintDesc = "Tint of the tail's base. " + colorDesc;
            const string tipTintDesc = "Tint of the tail's tip. " + colorDesc;

            // Add labels and boxes
            tab.AddItems(
                new OpRect (new Vector2(141f, 169f), new Vector2(319f, 262f)),
                new OpLabel(new Vector2(148f, 389f), new Vector2(305f, 35f), $"Player {ply + 1} Custom Tail", bigText: true),
                new OpLabel(new Vector2(148f, 360f), new Vector2(50f , 24f), "Sprite:"),
                new OpLabel(new Vector2(148f, 331f), new Vector2(150f, 24f), "Base Tint") { description = baseTintDesc },
                new OpLabel(new Vector2(303f, 331f), new Vector2(150f, 24f), "Tip Tint") { description = tipTintDesc }
            );

            // Sprite
            _sprites[ply] = new OpTextBox(new Vector2(203f, 360f), 200f, $"CTail{ply}Sprite", defaults.sprite);
            tab.AddItems(_sprites[ply]);

            // Colors
            tab.AddItems(
                new OpColorPicker(new Vector2(148f, 176f), $"CTail{ply}BaseTint", OpColorPicker.ColorToHex(defaults.baseTint)) { description = baseTintDesc },
                new OpColorPicker(new Vector2(303f, 176f), $"CTail{ply}TipTint", OpColorPicker.ColorToHex(defaults.tipTint)) { description = tipTintDesc }
            );
        }

        public override bool Configuable() => true;

        private string[] _lastSpriteNames;
        public override void Update(float dt)
        {
            base.Update(dt);
            if(_lastSpriteNames == null)
            {
                _lastSpriteNames = new string[CustomTail.configs.Length];
            }
            for (int i = 0; i < CustomTail.configs.Length; i++)
            {
                OpTextBox box = _sprites[i];
                if (box.value != _lastSpriteNames[i])
                {
                    // Check if the sprite is valid
                    _lastSpriteNames[i] = box.value;
                    box.colorText = Futile.atlasManager.DoesContainElementWithName(box.value) ? Color.white : Color.red;
                    box.bumpBehav.flash = 0f;
                }
            }
        }

        public override void ConfigOnChange()
        {
            base.ConfigOnChange();
            for(int i = 0; i < CustomTail.configs.Length; i++)
            {
                TailConfig c = new TailConfig();
                c.sprite = config[$"CTail{i}Sprite"];
                c.baseTint = TrueHexToColor(config[$"CTail{i}BaseTint"]);
                c.tipTint = TrueHexToColor(config[$"CTail{i}TipTint"]);
                CustomTail.configs[i] = c;
            }
        }

        private Color TrueHexToColor(string input)
        {
            if (input == "000000") return Color.black;
            return OpColorPicker.HexToColor(input);
        }
    }
}
