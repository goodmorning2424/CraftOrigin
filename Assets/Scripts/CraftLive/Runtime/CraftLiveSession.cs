using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace CraftOrigin.CraftLive
{
    [DefaultExecutionOrder(-300)]
    public sealed class CraftLiveSession : MonoBehaviour
    {
        [SerializeField] private CraftLiveCatalog catalog;
        [SerializeField] private CraftLiveRules rules;
        [SerializeField] private string roomId = "001";
        [SerializeField] private CraftLiveRole role = CraftLiveRole.Auto;
        [SerializeField] private UnityEvent<string> onMessageChanged;

        private CraftLiveRoomState state;

        public CraftLiveCatalog Catalog => catalog;
        public CraftLiveRules Rules => rules;
        public CraftLiveRoomState State => state;
        public string RoomId => roomId;
        public CraftLiveRole Role => role;

        public event Action<CraftLiveRoomState> StateChanged;
        public event Action<CraftLiveRoomState> LocalStateChanged;

        private void Awake()
        {
            state = CraftLiveRoomState.Create(catalog);
        }

        [ContextMenu("Validate Craft-live Configuration")]
        public void ValidateConfiguration()
        {
            if (catalog == null)
            {
                Debug.LogError("CraftLiveSession: Catalogが設定されていません。", this);
                return;
            }

            if (rules == null)
            {
                Debug.LogError("CraftLiveSession: Rulesが設定されていません。", this);
                return;
            }

            HashSet<string> ids = new HashSet<string>();
            foreach (CraftLiveMaterialDefinition material in catalog.Materials)
            {
                if (material == null || string.IsNullOrWhiteSpace(material.MaterialId))
                {
                    Debug.LogError("CraftLiveSession: 無効なMaterial定義があります。", this);
                    return;
                }

                if (!ids.Add($"material:{material.MaterialId}"))
                {
                    Debug.LogError($"CraftLiveSession: Material IDが重複しています: {material.MaterialId}", this);
                    return;
                }
            }

            foreach (CraftLiveWeaponDefinition weapon in catalog.Weapons)
            {
                if (weapon == null || string.IsNullOrWhiteSpace(weapon.WeaponId))
                {
                    Debug.LogError("CraftLiveSession: 無効なWeapon定義があります。", this);
                    return;
                }

                if (!ids.Add($"weapon:{weapon.WeaponId}"))
                {
                    Debug.LogError($"CraftLiveSession: Weapon IDが重複しています: {weapon.WeaponId}", this);
                    return;
                }
            }

            Debug.Log("CraftLiveSession: 設定に問題はありません。", this);
        }

        public void Configure(string newRoomId, CraftLiveRole newRole)
        {
            if (!string.IsNullOrWhiteSpace(newRoomId))
            {
                roomId = newRoomId.Trim();
            }

            if (newRole != CraftLiveRole.Auto)
            {
                role = newRole;
            }
        }

        public void ApplyRemoteState(CraftLiveRoomState remoteState)
        {
            if (remoteState == null)
            {
                return;
            }

            remoteState.Normalize(catalog);
            state = remoteState;
            if (ShouldRestartExpiredEmptySession(state))
            {
                RestartExpiredEmptySession();
                return;
            }

            StateChanged?.Invoke(state);
            onMessageChanged?.Invoke(state.message);
        }

        private bool ShouldRestartExpiredEmptySession(
            CraftLiveRoomState candidate)
        {
            return candidate != null &&
                   candidate.sessionEndsAtUnixMs > 0 &&
                   candidate.sessionEndsAtUnixMs <= UnixNowMs() &&
                   candidate.completedWeapons.Count == 0 &&
                   candidate.craft.status != CraftLiveCraftStatus.Complete;
        }

        private void RestartExpiredEmptySession()
        {
            long previousRevision = state.revision;
            long now = UnixNowMs();
            long durationMs = Mathf.RoundToInt(
                (rules != null ? rules.SessionDurationSeconds : 300f) *
                1000f);
            CraftLiveRoomState next = CraftLiveRoomState.Create(catalog);
            next.sessionStartedAtUnixMs = now;
            next.sessionEndsAtUnixMs = now + durationMs;
            next.sessionPhase = CraftLiveSessionPhase.Playing;
            next.revision = previousRevision + 1;
            next.updatedAtUnixMs = now;
            next.message = "新しい制作を開始しました。";
            state = next;
            PublishLocal();
        }

        public bool IsMaterialUnlocked(CraftLiveMaterialDefinition material)
        {
            if (material == null)
            {
                return false;
            }

            return !material.RequiresQrUnlock ||
                   state.HasMaterialRegistered(material.MaterialId);
        }

        public int GetMaterialCount(string materialId)
        {
            return state != null ? state.GetInventoryCount(materialId) : 0;
        }

        public void EnsureSessionStarted()
        {
            if (state == null ||
                state.sessionStartedAtUnixMs > 0)
            {
                return;
            }

            long now = UnixNowMs();
            long durationMs = Mathf.RoundToInt(
                (rules != null
                    ? rules.SessionDurationSeconds
                    : 300f) * 1000f);
            Mutate(next =>
            {
                next.sessionStartedAtUnixMs = now;
                next.sessionEndsAtUnixMs = now + durationMs;
                next.sessionPhase =
                    CraftLiveSessionPhase.Playing;
                next.message = "武器づくりを始めよう";
            });
        }

        public float GetRemainingSessionSeconds()
        {
            if (state == null ||
                state.sessionEndsAtUnixMs <= 0)
            {
                return rules != null
                    ? rules.SessionDurationSeconds
                    : 300f;
            }

            return Mathf.Max(
                0f,
                (state.sessionEndsAtUnixMs - UnixNowMs()) /
                1000f);
        }

        public void ExpireSession()
        {
            if (state == null ||
                state.sessionPhase !=
                    CraftLiveSessionPhase.Playing)
            {
                return;
            }

            Mutate(next =>
            {
                next.sessionPhase =
                    CraftLiveSessionPhase.FinalSelection;
                next.selectedMaterialId = string.Empty;
                next.transferQueue.Clear();
                next.transferBatchRemaining = 0;
                next.placement.Clear();
                next.message = next.completedWeapons.Count > 0
                    ? "完成武器を1つ選んでください"
                    : "完成した武器がありません";
            });
        }

        public bool CanPlaceSelectedMaterialIn(CraftLiveSlotId slot)
        {
            CraftLiveMaterialDefinition material = catalog != null
                ? catalog.FindMaterial(state.selectedMaterialId)
                : null;
            return material != null &&
                   state.placement.status == CraftLivePlacementStatus.SelectingSlot &&
                   material.CanUseIn(slot) &&
                   state.CanReserveSlot(slot);
        }

        public void SelectMaterial(CraftLiveMaterialDefinition material)
        {
            if (!CanContinuePlaying())
            {
                return;
            }

            if (material == null)
            {
                SetMessage("素材が設定されていません。");
                return;
            }

            if (!IsMaterialUnlocked(material))
            {
                SetMessage($"{material.DisplayName}はQRコードで解放してください。");
                return;
            }

            if (!state.weaponSelectionConfirmed)
            {
                SetMessage(
                    "エラー：先にパッド2で武器を選択して確定してください。");
                return;
            }

            if (state.placement.status != CraftLivePlacementStatus.Idle)
            {
                SetMessage("現在の配置を完了またはキャンセルしてください。");
                return;
            }

            Mutate(next =>
            {
                next.selectedMaterialId = material.MaterialId;
                next.placement.Clear();
                next.placement.materialId = material.MaterialId;
                next.placement.status = CraftLivePlacementStatus.SelectingSlot;
                next.placement.statusChangedAtUnixMs = UnixNowMs();
                next.message = "Pad2で配置場所を選んでください。";
            });
            CraftLiveAudio.Play(CraftLiveSound.MaterialSelect, 0.78f);
        }

        public void ChoosePlacementSlot(CraftLiveSlotId slot)
        {
            CraftLiveMaterialDefinition material = catalog != null
                ? catalog.FindMaterial(state.selectedMaterialId)
                : null;
            if (material == null)
            {
                SetMessage("先に素材を選択してください。");
                return;
            }

            if (state.placement.status != CraftLivePlacementStatus.SelectingSlot &&
                state.placement.status != CraftLivePlacementStatus.ConfirmingSlot)
            {
                SetMessage("今は配置場所を選択できません。");
                return;
            }

            if (!material.CanUseIn(slot))
            {
                SetMessage($"{material.DisplayName}は{CraftLiveSlot.ToKey(slot)}スロットへ配置できません。");
                return;
            }

            if (!state.CanReserveSlot(slot))
            {
                SetMessage("その配置枠は使用済みまたは転送待ちです。");
                return;
            }

            Mutate(next =>
            {
                next.placement.hasCandidateSlot = true;
                next.placement.candidateSlot = slot;
                next.placement.status = CraftLivePlacementStatus.ConfirmingSlot;
                next.placement.statusChangedAtUnixMs = UnixNowMs();
                next.message = "この場所に置きますか？";
            });
            CraftLiveAudio.Play(CraftLiveSound.Select, 0.72f);
        }

        public void ClearPlacementChoice()
        {
            if (state.placement.status != CraftLivePlacementStatus.ConfirmingSlot)
            {
                return;
            }

            Mutate(next =>
            {
                next.placement.hasCandidateSlot = false;
                next.placement.status = CraftLivePlacementStatus.SelectingSlot;
                next.placement.statusChangedAtUnixMs = UnixNowMs();
                next.message = "どこに置く？";
            });
        }

        public void CancelPlacement()
        {
            if (state.placement.status != CraftLivePlacementStatus.SelectingSlot &&
                state.placement.status != CraftLivePlacementStatus.ConfirmingSlot)
            {
                return;
            }

            Mutate(next =>
            {
                next.selectedMaterialId = string.Empty;
                next.placement.Clear();
                next.message = "素材をタップしてください。";
            });
            CraftLiveAudio.Play(CraftLiveSound.Cancel, 0.82f);
        }

        public void ConfirmPlacement()
        {
            if (state.placement.status != CraftLivePlacementStatus.ConfirmingSlot ||
                !state.placement.hasCandidateSlot)
            {
                SetMessage("先に配置場所を選択してください。");
                return;
            }

            CraftLiveMaterialDefinition material = catalog != null
                ? catalog.FindMaterial(state.placement.materialId)
                : null;
            if (material == null || !IsMaterialUnlocked(material))
            {
                SetMessage("選択した素材の所持数が足りません。");
                return;
            }

            CraftLiveSlotId confirmedSlot =
                state.placement.candidateSlot;
            if (!state.CanReserveSlot(confirmedSlot))
            {
                SetMessage("その配置枠は使用済みまたは転送待ちです。");
                return;
            }

            Mutate(next =>
            {
                next.transferQueueSerial++;
                next.transferQueue.Add(
                    new CraftLiveTransferQueueEntry(
                        next.transferQueueSerial,
                        material.MaterialId,
                        confirmedSlot));
                next.selectedMaterialId = string.Empty;
                next.placement.Clear();
                next.message =
                    $"転送待ちに追加しました（{next.transferQueue.Count}個）";
            });
            CraftLiveAudio.Play(CraftLiveSound.Confirm, 0.86f);
        }

        public bool BeginSingleTransfer()
        {
            return BeginTransferBatch(1);
        }

        public bool BeginAllQueuedTransfers()
        {
            return BeginTransferBatch(
                state != null && state.transferQueue != null
                    ? state.transferQueue.Count
                    : 0);
        }

        public bool BeginTransferBatch(int requestedCount)
        {
            if (!CanContinuePlaying())
            {
                return false;
            }

            if (state == null ||
                state.placement.status !=
                    CraftLivePlacementStatus.Idle ||
                state.transferQueue == null ||
                state.transferQueue.Count == 0)
            {
                SetMessage("転送待ちの素材がありません。");
                return false;
            }

            int count = Mathf.Clamp(
                requestedCount,
                1,
                state.transferQueue.Count);
            Mutate(next =>
            {
                next.transferBatchSerial++;
                next.transferBatchRemaining = count - 1;
                ActivateNextQueuedTransfer(
                    next,
                    CraftLivePlacementStatus.Pad1Loading);
                next.message = count > 1
                    ? $"{count}個をまとめて転送します"
                    : "1個を転送します";
            });
            return true;
        }

        public void CancelQueuedTransfer(int serial)
        {
            if (state == null ||
                state.placement.status !=
                    CraftLivePlacementStatus.Idle)
            {
                return;
            }

            Mutate(next =>
            {
                next.transferQueue.RemoveAll(
                    entry => entry != null &&
                             entry.serial == serial);
                next.message =
                    $"転送待ち: {next.transferQueue.Count}個";
            });
        }

        public void ClearTransferQueue()
        {
            if (state == null ||
                state.placement.status !=
                    CraftLivePlacementStatus.Idle)
            {
                return;
            }

            Mutate(next =>
            {
                next.transferQueue.Clear();
                next.transferBatchRemaining = 0;
                next.message = "転送待ちを取り消しました";
            });
        }

        public void MarkTransferLaunching()
        {
            if (state.placement.status ==
                    CraftLivePlacementStatus.Idle &&
                state.transferQueue != null &&
                state.transferQueue.Count > 0)
            {
                Mutate(next =>
                {
                    next.transferBatchSerial++;
                    next.transferBatchRemaining = 0;
                    ActivateNextQueuedTransfer(
                        next,
                        CraftLivePlacementStatus.Pad1Launching);
                    next.message = "転送中";
                });
                return;
            }

            if (state.placement.status != CraftLivePlacementStatus.Pad1Loading)
            {
                return;
            }

            Mutate(next =>
            {
                next.placement.status = CraftLivePlacementStatus.Pad1Launching;
                next.placement.statusChangedAtUnixMs = UnixNowMs();
                next.message = "転送中";
            });
        }

        public void MarkTransferArriving()
        {
            if (state.placement.status != CraftLivePlacementStatus.Pad1Launching)
            {
                return;
            }

            Mutate(next =>
            {
                next.placement.status = CraftLivePlacementStatus.Pad2Arriving;
                next.placement.statusChangedAtUnixMs = UnixNowMs();
                next.message = "素材が到着します";
            });
        }

        public void CompleteTransferPreviewWithoutPlacement()
        {
            if (state.placement.status !=
                CraftLivePlacementStatus.Pad2Arriving)
            {
                return;
            }

            Mutate(next =>
            {
                if (next.transferBatchRemaining > 0 &&
                    next.transferQueue.Count > 0)
                {
                    next.transferBatchRemaining--;
                    ActivateNextQueuedTransfer(
                        next,
                        CraftLivePlacementStatus.Pad1Loading);
                    next.message = "次の素材を転送します";
                }
                else
                {
                    next.transferBatchRemaining = 0;
                    next.selectedMaterialId = string.Empty;
                    next.placement.Clear();
                    next.message = next.transferQueue.Count > 0
                        ? $"転送待ち: {next.transferQueue.Count}個"
                        : "次の素材を選んでください";
                }
            });
        }

        public void CompleteCurrentPlacement()
        {
            if (state.placement.status != CraftLivePlacementStatus.Pad2Arriving ||
                !state.placement.hasConfirmedSlot)
            {
                return;
            }

            Mutate(next =>
            {
                string materialId = next.placement.materialId;
                CraftLiveSlotId slot = next.placement.confirmedSlot;
                next.slots.Set(slot, materialId);
                next.placement.status = CraftLivePlacementStatus.PlacementComplete;
                next.placement.statusChangedAtUnixMs = UnixNowMs();
                next.message = "配置完了";
            });
        }

        public void ContinueAfterPlacement()
        {
            if (state.placement.status != CraftLivePlacementStatus.PlacementComplete)
            {
                return;
            }

            Mutate(next =>
            {
                if (next.transferBatchRemaining > 0 &&
                    next.transferQueue.Count > 0)
                {
                    next.transferBatchRemaining--;
                    ActivateNextQueuedTransfer(
                        next,
                        CraftLivePlacementStatus.Pad1Loading);
                    next.message = "次の素材を転送準備中";
                }
                else
                {
                    next.transferBatchRemaining = 0;
                    next.selectedMaterialId = string.Empty;
                    next.placement.Clear();
                    next.message = next.transferQueue.Count > 0
                        ? $"転送待ち: {next.transferQueue.Count}個"
                        : "次の素材を選ぼう";
                }
            });
        }

        public void RemoveSlot(CraftLiveSlotId slot)
        {
            if (state.placement.status != CraftLivePlacementStatus.Idle)
            {
                SetMessage("素材の転送中は取り外せません。");
                return;
            }

            Mutate(next =>
            {
                next.slots.Set(slot, string.Empty);
                PublishStatsToPad3(next);
                next.message = $"{CraftLiveSlot.ToKey(slot)}スロットを空にしました。";
            });
        }

        public void SelectWeapon(CraftLiveWeaponDefinition weapon)
        {
            if (weapon == null)
            {
                SetMessage("武器が設定されていません。");
                return;
            }

            Mutate(next =>
            {
                ClearUnconfirmedMaterialSelection(next);
                next.selectedWeaponId = weapon.WeaponId;
                next.weaponSelectionConfirmed = false;
                next.message = $"{weapon.DisplayName}を選択しました。";
            });
            CraftLiveAudio.Play(CraftLiveSound.Select, 0.76f);
        }

        public void ConfirmWeapon(CraftLiveWeaponDefinition weapon)
        {
            if (weapon == null)
            {
                SetMessage("武器が設定されていません。");
                return;
            }

            Mutate(next =>
            {
                ClearUnconfirmedMaterialSelection(next);
                next.selectedWeaponId = weapon.WeaponId;
                next.weaponSelectionConfirmed = true;
                PublishStatsToPad3(next);
                next.message = $"{weapon.DisplayName}を合成対象に確定しました。";
            });
            CraftLiveAudio.Play(CraftLiveSound.Confirm, 0.88f);
        }

        public void PublishCurrentStatsToPad3()
        {
            if (state == null)
            {
                return;
            }

            Mutate(next => PublishStatsToPad3(next));
        }

        public bool StartSynthesis()
        {
            if (!CanContinuePlaying())
            {
                return false;
            }

            string error = CraftLiveCalculator.ValidateSynthesis(
                state,
                catalog,
                rules);
            if (!string.IsNullOrEmpty(error))
            {
                SetMessage(error);
                return false;
            }

            Mutate(next =>
            {
                next.craft.status = CraftLiveCraftStatus.Mixing;
                next.craft.mixPower = 0f;
                next.craft.mixBonus = 0f;
                next.craft.hammerPassCount = 0;
                next.craft.resultRank = "通常成功";
                next.craft.startedAtUnixMs = UnixNowMs();
                next.message = "ぐるぐる合成を開始しました。";
            });
            CraftLiveAudio.StartSynthesisLoop();
            return true;
        }

        public bool RegisterHammerPass(
            float quality = 1f,
            bool completeImmediately = true)
        {
            if (state == null ||
                state.craft.status !=
                    CraftLiveCraftStatus.Mixing)
            {
                return false;
            }

            int required = rules != null
                ? rules.RequiredHammerPasses
                : 6;
            quality = Mathf.Clamp01(quality);
            Mutate(next =>
            {
                next.craft.hammerPassCount++;
                float progress = Mathf.Clamp01(
                    next.craft.hammerPassCount /
                    (float)required);
                next.craft.mixPower = Mathf.Max(
                    next.craft.mixPower,
                    progress * 100f *
                    Mathf.Lerp(0.75f, 1f, quality));
                CraftLiveRank rank = rules != null
                    ? rules.EvaluateRank(
                        next.craft.mixPower)
                    : new CraftLiveRank(
                        "通常成功",
                        0f);
                next.craft.mixBonus = rank.Bonus;
                next.craft.resultRank = rank.Name;
                next.message =
                    $"鍛錬 {next.craft.hammerPassCount}/{required}";
            });
            CraftLiveAudio.Play(CraftLiveSound.HammerStrike, 0.94f);

            if (state.craft.hammerPassCount >= required)
            {
                if (completeImmediately)
                {
                    CompleteSynthesis();
                }
                return true;
            }

            return false;
        }

        public void SetMixPower(float power)
        {
            if (state.craft.status != CraftLiveCraftStatus.Mixing)
            {
                return;
            }

            Mutate(next =>
            {
                next.craft.mixPower = Mathf.Clamp(power, 0f, 100f);
                CraftLiveRank rank = rules != null
                    ? rules.EvaluateRank(next.craft.mixPower)
                    : new CraftLiveRank("通常成功", 0f);
                next.craft.mixBonus = rank.Bonus;
                next.craft.resultRank = rank.Name;
                next.message = $"魔力充填率 {Mathf.RoundToInt(next.craft.mixPower)}% / {rank.Name}";
            });
        }

        public void CompleteSynthesis(bool deferPresentation = false)
        {
            if (state.craft.status != CraftLiveCraftStatus.Mixing)
            {
                return;
            }

            string error = CraftLiveCalculator.ValidateSynthesis(
                state,
                catalog,
                rules);
            if (!string.IsNullOrEmpty(error))
            {
                SetMessage(error);
                return;
            }

            Mutate(next =>
            {
                next.result = CraftLiveCalculator.BuildResult(next, catalog, rules, UnixNowMs());
                next.craft.status = CraftLiveCraftStatus.Complete;
                next.craft.mixBonus = rules != null
                    ? rules.EvaluateRank(next.craft.mixPower).Bonus
                    : 0f;
                next.craft.resultRank = next.result.rank;
                next.craft.completionPresentationReady =
                    !deferPresentation;
                next.completedWeapons.Add(next.result.Clone());
                int maximum = rules != null
                    ? rules.MaximumCompletedWeapons
                    : 12;
                while (next.completedWeapons.Count > maximum)
                {
                    next.completedWeapons.RemoveAt(0);
                }

                if (!deferPresentation)
                {
                    PublishStatsToPad3(next);
                }
                if (next.sessionEndsAtUnixMs > 0 &&
                    UnixNowMs() >= next.sessionEndsAtUnixMs)
                {
                    next.sessionPhase =
                        CraftLiveSessionPhase.FinalSelection;
                }

                next.message = $"合成{next.result.rank}！ {next.result.weaponName}が完成しました。";
            });
            if (!deferPresentation)
            {
                CraftLiveAudio.StopSynthesisLoop();
                CraftLiveAudio.PlayForgeComplete();
            }
        }

        public void RevealCompletionPresentation()
        {
            if (state == null ||
                state.craft.status != CraftLiveCraftStatus.Complete ||
                state.craft.completionPresentationReady)
            {
                return;
            }

            Mutate(next =>
            {
                next.craft.completionPresentationReady = true;
                PublishStatsToPad3(next);
            });
            CraftLiveAudio.StopSynthesisLoop();
            CraftLiveAudio.PlayForgeComplete();
        }

        public void BeginNextWeapon()
        {
            if (state == null ||
                state.sessionPhase !=
                    CraftLiveSessionPhase.Playing ||
                state.craft.status !=
                    CraftLiveCraftStatus.Complete)
            {
                return;
            }

            Mutate(next =>
            {
                next.selectedMaterialId = string.Empty;
                next.placement.Clear();
                next.transferQueue.Clear();
                next.transferBatchRemaining = 0;
                next.slots.Clear();
                next.displayedStats = new CraftLiveStats();
                next.statusDisplaySerial++;
                next.weaponSelectionConfirmed = false;
                next.craft = new CraftLiveCraftState();
                next.result = new CraftLiveResultState();
                next.message = "次の武器を選ぼう";
            });
        }

        public bool SelectFinalWeapon(int resultSerial)
        {
            if (state == null ||
                state.sessionPhase ==
                    CraftLiveSessionPhase.Playing ||
                state.completedWeapons == null)
            {
                return false;
            }

            CraftLiveResultState selected = null;
            foreach (CraftLiveResultState completed in
                     state.completedWeapons)
            {
                if (completed != null &&
                    completed.resultSerial == resultSerial)
                {
                    selected = completed;
                    break;
                }
            }

            if (selected == null)
            {
                SetMessage("選択した完成武器が見つかりません。");
                return false;
            }

            string prefix = rules != null
                ? rules.WeaponCodePrefix
                : "CL";
            string code = CraftLiveWeaponCode.Generate(
                prefix,
                roomId,
                selected);
            Mutate(next =>
            {
                next.selectedFinalResultSerial =
                    selected.resultSerial;
                next.finalWeaponCode = code;
                next.result = selected.Clone();
                next.sessionPhase =
                    CraftLiveSessionPhase.Finished;
                next.message =
                    $"完成しました。次の部屋に進んでください。武器コード: {code}";
            });
            return true;
        }

        public void UnlockMaterialId(string materialId)
        {
            CraftLiveMaterialDefinition material = catalog != null
                ? catalog.FindMaterial(materialId)
                : null;
            if (material == null)
            {
                SetMessage("このQRコードはCraft-liveの素材ではありません。");
                return;
            }

            Mutate(next =>
            {
                bool newlyRegistered =
                    next.RegisterMaterial(material.MaterialId);
                next.lastRegisteredMaterialId = material.MaterialId;
                next.lastRegistrationDelta = newlyRegistered ? 1 : 0;
                next.registrationSerial++;
                next.message = newlyRegistered
                    ? $"{material.DisplayName}を登録しました"
                    : $"{material.DisplayName}は登録済みです";
            });
        }

        /// <summary>
        /// Starts a fresh group. QR registrations are intentionally scoped to
        /// the current group and must not unlock materials for the next one.
        /// </summary>
        public void ResetRoomForNextGroup()
        {
            CraftLiveRoomState next = CraftLiveRoomState.Create(catalog);
            next.message =
                "次のグループを開始します。武器を選んでください。";
            next.revision = state.revision + 1;
            next.updatedAtUnixMs = UnixNowMs();
            state = next;
            PublishLocal();
        }

        public string GetInstruction(CraftLiveRole targetRole)
        {
            if (state == null)
            {
                return string.Empty;
            }

            switch (state.placement.status)
            {
                case CraftLivePlacementStatus.SelectingSlot:
                    return targetRole == CraftLiveRole.WorkbenchPad
                        ? "どこに置く？"
                        : "Pad2で配置場所を選択";
                case CraftLivePlacementStatus.ConfirmingSlot:
                    return targetRole == CraftLiveRole.WorkbenchPad
                        ? "この場所に置きますか？"
                        : "Pad2で確認";
                case CraftLivePlacementStatus.Pad1Loading:
                    return "転送準備中";
                case CraftLivePlacementStatus.Pad1Launching:
                case CraftLivePlacementStatus.Pad2Arriving:
                    return "転送中";
                case CraftLivePlacementStatus.PlacementComplete:
                    return targetRole == CraftLiveRole.WorkbenchPad
                        ? "配置完了"
                        : "次の素材を選ぼう";
                default:
                    return targetRole == CraftLiveRole.WorkbenchPad
                        ? "Pad1で素材を選ぼう"
                        : "素材をタップ";
            }
        }

        public CraftLiveStats CalculateCurrentStats()
        {
            float bonus = state.craft.status == CraftLiveCraftStatus.Complete
                ? state.craft.mixBonus
                : 0f;
            return CraftLiveCalculator.CalculateStats(state, catalog, rules, bonus);
        }

        private static void ActivateNextQueuedTransfer(
            CraftLiveRoomState target,
            CraftLivePlacementStatus status)
        {
            CraftLiveTransferQueueEntry entry =
                target.transferQueue[0];
            target.transferQueue.RemoveAt(0);
            target.selectedMaterialId = string.Empty;
            target.placement.Clear();
            target.placement.materialId = entry.materialId;
            target.placement.hasConfirmedSlot = true;
            target.placement.confirmedSlot = entry.slot;
            target.placement.transferSerial = entry.serial;
            target.placement.status = status;
            target.placement.statusChangedAtUnixMs = UnixNowMs();
        }

        private void PublishStatsToPad3(
            CraftLiveRoomState target)
        {
            float bonus =
                target.craft.status ==
                    CraftLiveCraftStatus.Complete
                    ? target.craft.mixBonus
                    : 0f;
            target.displayedStats =
                CraftLiveCalculator.CalculateStats(
                    target,
                    catalog,
                    rules,
                    bonus);
            target.statusDisplaySerial++;
        }

        private void SetMessage(string message)
        {
            Mutate(next => next.message = message);
        }

        private static void ClearUnconfirmedMaterialSelection(
            CraftLiveRoomState target)
        {
            if (target == null || target.weaponSelectionConfirmed)
            {
                return;
            }

            if (target.placement.status !=
                    CraftLivePlacementStatus.SelectingSlot &&
                target.placement.status !=
                    CraftLivePlacementStatus.ConfirmingSlot)
            {
                return;
            }

            target.selectedMaterialId = string.Empty;
            target.placement.Clear();
        }

        private bool CanContinuePlaying()
        {
            if (state != null &&
                state.sessionPhase ==
                    CraftLiveSessionPhase.Playing)
            {
                return true;
            }

            SetMessage("制限時間が終了しています。");
            return false;
        }

        private void Mutate(Action<CraftLiveRoomState> mutation)
        {
            CraftLiveRoomState next = state.Clone();
            mutation(next);
            next.Normalize(catalog);
            next.revision = Math.Max(state.revision + 1, next.revision + 1);
            next.updatedAtUnixMs = UnixNowMs();
            state = next;
            PublishLocal();
        }

        private void PublishLocal()
        {
            StateChanged?.Invoke(state);
            LocalStateChanged?.Invoke(state);
            onMessageChanged?.Invoke(state.message);
        }

        public static long UnixNowMs()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
