using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace WildTerraHook
{
    public class AutoHealModule
    {
        private float _nextCheck = 0f;
        private float _nextHealTime = 0f; // Czas, kiedy będziemy mogli znowu się uleczyć
        private WTPlayer _localPlayer;

        public void Update()
        {
            if (!ConfigManager.Heal_Enabled) return;

            // Jeśli timer cooldownu jeszcze trwa, nie robimy nic
            if (Time.time < _nextHealTime) return;

            // Sprawdzaj warunki co 0.5s (optymalizacja)
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + 0.5f;

            if (_localPlayer == null || !_localPlayer.isLocalPlayer)
                _localPlayer = GetLocalPlayer();

            if (_localPlayer == null) return;

            // 1. Sprawdzenie walki
            if (ConfigManager.Heal_CombatOnly)
            {
                if (_localPlayer.target == null) return;
            }

            // 2. Sprawdzenie HP
            if (_localPlayer.healthMax <= 0) return;
            float hpPercent = (float)_localPlayer.health / (float)_localPlayer.healthMax * 100f;

            // Jeśli HP jest powyżej limitu (np. 60%), nie leczymy
            if (hpPercent >= ConfigManager.Heal_Percent) return;

            // 3. Użycie przedmiotu i ustawienie Timera
            UseHealItem(_localPlayer);
        }

        private void UseHealItem(WTPlayer player)
        {
            if (string.IsNullOrEmpty(ConfigManager.Heal_ItemName)) return;

            for (int i = 0; i < player.inventory.Count; i++)
            {
                var slot = player.inventory[i];
                if (slot.amount > 0 && slot.item.data != null)
                {
                    if (slot.item.data.name.IndexOf(ConfigManager.Heal_ItemName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        try
                        {
                            // Użycie: slot, akcja, zakładka 0
                            player.CmdInventoryItemAction(i, ItemActionType.Use, 0);

                            // Ustawiamy timer na przyszłość: Czas teraz + Czas z suwaka
                            _nextHealTime = Time.time + ConfigManager.Heal_Cooldown;
                        }
                        catch
                        {
                            Debug.LogError("[AutoHeal] Błąd CmdInventoryItemAction");
                        }
                        return; // Wychodzimy po użyciu jednego bandaża
                    }
                }
            }
        }

        private WTPlayer GetLocalPlayer()
        {
            foreach (WTPlayer p in Object.FindObjectsOfType<WTPlayer>())
            {
                if (p.isLocalPlayer) return p;
            }
            return null;
        }

        public void DrawMenu()
        {
            GUILayout.Label("<b>" + Localization.Get("HEAL_HEADER") + "</b>");

            bool en = GUILayout.Toggle(ConfigManager.Heal_Enabled, " " + Localization.Get("HEAL_ENABLE"));
            if (en != ConfigManager.Heal_Enabled) { ConfigManager.Heal_Enabled = en; ConfigManager.Save(); }

            if (ConfigManager.Heal_Enabled)
            {
                GUILayout.BeginVertical("box");

                GUILayout.Label(Localization.Get("HEAL_ITEM_NAME"));
                string newItem = GUILayout.TextField(ConfigManager.Heal_ItemName);
                if (newItem != ConfigManager.Heal_ItemName) { ConfigManager.Heal_ItemName = newItem; ConfigManager.Save(); }

                GUILayout.Space(5);
                GUILayout.Label($"{Localization.Get("HEAL_HP_PERCENT")}: {ConfigManager.Heal_Percent}%");
                int newPerc = (int)GUILayout.HorizontalSlider(ConfigManager.Heal_Percent, 10, 90);
                if (newPerc != ConfigManager.Heal_Percent) { ConfigManager.Heal_Percent = newPerc; ConfigManager.Save(); }

                // Nowy Suwak - Cooldown
                GUILayout.Space(5);
                GUILayout.Label($"{Localization.Get("HEAL_COOLDOWN")}: {ConfigManager.Heal_Cooldown:F1}s");
                float newCd = GUILayout.HorizontalSlider(ConfigManager.Heal_Cooldown, 5.0f, 60.0f);
                if (Mathf.Abs(newCd - ConfigManager.Heal_Cooldown) > 0.1f)
                {
                    ConfigManager.Heal_Cooldown = newCd;
                    ConfigManager.Save();
                }

                GUILayout.Space(5);
                bool combat = GUILayout.Toggle(ConfigManager.Heal_CombatOnly, " " + Localization.Get("HEAL_COMBAT_ONLY"));
                if (combat != ConfigManager.Heal_CombatOnly) { ConfigManager.Heal_CombatOnly = combat; ConfigManager.Save(); }

                GUILayout.EndVertical();
            }
        }
    }
}