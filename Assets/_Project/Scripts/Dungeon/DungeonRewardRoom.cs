using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Caos.Maps;

namespace Caos.Dungeon
{
    /// <summary>Cámara del tesoro: tres reliquias, una elección con 1 / 2 / 3.</summary>
    public class DungeonRewardRoom : DungeonRoom
    {
        readonly List<DungeonRelicDefinition> _choices = new List<DungeonRelicDefinition>();

        public override string ProgressText
        {
            get
            {
                if (_choices.Count == 0) return "Preparando reliquias…";
                string text = "1) " + _choices[0].title;
                if (_choices.Count > 1) text += "\n2) " + _choices[1].title;
                if (_choices.Count > 2) text += "\n3) " + _choices[2].title;
                return text;
            }
        }

        protected override void OnBegin()
        {
            _choices.Clear();
            var available = new List<DungeonRelicDefinition>();
            if (Director.Config.relics != null)
            {
                for (int i = 0; i < Director.Config.relics.Length; i++)
                    if (Director.Config.relics[i] != null) available.Add(Director.Config.relics[i]);
            }

            int pickCount = Mathf.Min(3, available.Count);
            for (int i = 0; i < pickCount; i++)
            {
                int index = Director.RandomInt(0, available.Count);
                _choices.Add(available[index]);
                available.RemoveAt(index);
            }

            for (int i = 0; i < _choices.Count; i++)
            {
                Vector3 position = Center + Vector3.right * ((i - 1) * 5.2f)
                    + Vector3.forward * 2.4f;
                GameObject pedestal = DungeonVisuals.Cylinder("Pedestal", position
                    + Vector3.up * 0.35f, new Vector3(1.5f, 0.7f, 1.5f),
                    DungeonPalette.Wall, transform, true, true);
                DungeonVisuals.Sphere("RelicOrb", position + Vector3.up * 1.35f, 1f,
                    DungeonPalette.Relic(i), pedestal.transform, true, true);
                TrackObject(pedestal);
            }

            Director.ShowBanner("ELIGE UNA RELIQUIA: 1 / 2 / 3", 3.4f);
        }

        protected override void OnTick(float deltaTime)
        {
            if (Keyboard.current == null || _choices.Count == 0) return;

            if (Keyboard.current.digit1Key.wasPressedThisFrame) Select(0);
            else if (Keyboard.current.digit2Key.wasPressedThisFrame) Select(1);
            else if (Keyboard.current.digit3Key.wasPressedThisFrame) Select(2);
        }

        void Select(int index)
        {
            if (index < 0 || index >= _choices.Count || Completed || Failed) return;

            DungeonRelicDefinition relic = _choices[index];
            Director.ApplyRelic(relic);
            Director.ShowBanner($"{relic.title} — {relic.description}", 3.2f);
            ArenaAudio.Play(ArenaAudio.Sound.Upgrade, Center, 0.8f);
            ChaosFx.Burst(Center + Vector3.up, DungeonPalette.UiYellow, 24);
            Complete();
        }
    }
}
