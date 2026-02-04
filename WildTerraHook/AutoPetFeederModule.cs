using Mirror;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.AI;

namespace WildTerraHook
{
    public class AutoPetFeederModule
    {
        private float _lastGlobalFeedTime = 0f;

        // Anti-AFK
        private float _nextMoveTime = 0f;
        private bool _isMoving = false;

        private const float GLOBAL_FEED_DELAY = 1.0f; // 1s odstępu między pakietami

        public void Update()
        {
            // Debugger ekwipunku pod klawiszem 'O'
            if (Input.GetKeyDown(KeyCode.O)) DebugInventory();

            var player = global::Player.localPlayer as global::WTPlayer;
            if (player == null) return;

            // Niezależne sprawdzanie flag z ConfigManager
            if (ConfigManager.AntiAfk_Enabled) HandleAntiAfk(player);
            if (ConfigManager.AutoFeed_Enabled) HandleFeeding(player);
        }

        // --- DEBUGGER ---
        private void DebugInventory()
        {
            var player = global::Player.localPlayer as global::WTPlayer;
            if (player == null || player.inventory == null) return;

            Debug.LogWarning("=== DUMP EKWIPUNKU (PET DEBUG) ===");
            for (int i = 0; i < player.inventory.Count; i++)
            {
                var slot = player.inventory[i];
                if (slot.amount > 0 && slot.item.data != null)
                {
                    string info = $"Slot {i}: {slot.item.name}";
                    if (slot.item.itemValues != null)
                    {
                        foreach (var val in slot.item.itemValues) info += $" | {val.key}={val.value}";
                    }
                    Debug.Log(info);
                }
            }
            Debug.LogWarning("==================================");
        }

        // --- ANTI-AFK ---
        private void HandleAntiAfk(global::WTPlayer player)
        {
            if (Time.time > _nextMoveTime)
            {
                _nextMoveTime = Time.time + UnityEngine.Random.Range(20f, 40f);

                NavMeshAgent agent = player.GetComponent<NavMeshAgent>();
                if (agent != null && agent.isOnNavMesh)
                {
                    Vector3 randomOffset = new Vector3(UnityEngine.Random.Range(-1f, 1f), 0, UnityEngine.Random.Range(-1f, 1f));
                    agent.SetDestination(player.transform.position + randomOffset);
                    _isMoving = true;
                }
            }
            else if (_isMoving && Time.time > _nextMoveTime - 18f)
            {
                NavMeshAgent agent = player.GetComponent<NavMeshAgent>();
                if (agent != null && agent.isOnNavMesh && !agent.isStopped)
                {
                    agent.ResetPath();
                    _isMoving = false;
                }
            }
        }

        // --- FEEDING ---
        private void HandleFeeding(global::WTPlayer player)
        {
            if (Time.time < _lastGlobalFeedTime + GLOBAL_FEED_DELAY) return;
            if (player.inventory == null) return;

            for (int i = 0; i < player.inventory.Count; i++)
            {
                var slot = player.inventory[i];

                // Walidacja slotu
                if (slot.amount <= 0) continue;

                // Sprawdzamy czy wymaga karmienia (Smart Check)
                if (!IsHungry(slot.item)) continue;

                // Wyślij komendę karmienia
                FeedItem(player, i);
                return; // Przerwij pętlę, karmimy jednego na cykl (co 1s)
            }
        }

        private bool IsHungry(Item item)
        {
            // 1. SPRAWDZENIE STATUSU OSWOJENIA (PetFear)
            // Jeśli PetFear nie istnieje lub wynosi 0, zwierzę jest oswojone -> NIE KARMIĆ.
            string fearStr = GetItemValue(item, "PetFear");

            if (string.IsNullOrEmpty(fearStr))
            {
                // Brak klucza PetFear oznacza, że to albo nie jest pet do oswajania, albo już oswojony.
                return false;
            }

            if (int.TryParse(fearStr, out int fearValue))
            {
                if (fearValue <= 0) return false; // Strach 0 lub mniej = Oswojony.
            }

            // 2. SPRAWDZENIE CZASU KARMIENIA
            // Szukamy czasu następnego karmienia (priorytet dla Tamed, potem zwykły)
            double nextFeedTime = 0;
            bool hasFeedTime = false;

            string tamedTimeStr = GetItemValue(item, "TamedPetNextFeedTime");
            if (!string.IsNullOrEmpty(tamedTimeStr) && double.TryParse(tamedTimeStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double tTime))
            {
                nextFeedTime = tTime;
                hasFeedTime = true;
            }

            string petTimeStr = GetItemValue(item, "PetNextFeedTime");
            if (!string.IsNullOrEmpty(petTimeStr) && double.TryParse(petTimeStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double pTime))
            {
                // Jeśli mamy już czas z Tamed, bierzemy ten późniejszy (bezpieczniej)
                if (!hasFeedTime || pTime > nextFeedTime)
                {
                    nextFeedTime = pTime;
                    hasFeedTime = true;
                }
            }

            if (hasFeedTime)
            {
                // Jeśli czas serwera jest większy niż czas następnego karmienia -> Jest głodny
                if (NetworkTime.time >= nextFeedTime)
                {
                    return true;
                }
            }
            else
            {
                // Jest dziki (ma PetFear > 0), ale nie ma czasu karmienia.
                // To znaczy, że to pierwsze karmienie -> Tak, karmimy.
                return true;
            }

            return false;
        }

        private void FeedItem(global::WTPlayer player, int slotIndex)
        {
            try
            {
                var method = player.GetType().GetMethod("CmdInventoryItemAction",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);

                if (method != null)
                {
                    object actionVal = 5; // PetFeed
                    try
                    {
                        Type enumType = Type.GetType("ItemActionType, Assembly-CSharp");
                        if (enumType != null) actionVal = Enum.ToObject(enumType, 5);
                    }
                    catch { }

                    method.Invoke(player, new object[] { slotIndex, actionVal, 0 });

                    Debug.Log($"[AutoFeed] Wysłano karmienie dla slotu {slotIndex}.");
                    _lastGlobalFeedTime = Time.time;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AutoFeed] Błąd: {ex.Message}");
            }
        }

        // Helper do wyciągania wartości z Item (zastępuje item.GetValue jeśli brak metody rozszerzeń)
        private string GetItemValue(Item item, string key)
        {
            if (item.itemValues == null) return null;

            // SyncListStruct implementuje IEnumerable
            foreach (var val in item.itemValues)
            {
                if (val.key == key) return val.value;
            }
            return null;
        }
    }
}