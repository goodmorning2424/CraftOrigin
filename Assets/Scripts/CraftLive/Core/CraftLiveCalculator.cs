namespace CraftOrigin.CraftLive
{
    public static class CraftLiveCalculator
    {
        public const string SecretPikopikoWeaponId =
            "weapon_pikopiko_sword";
        public const string SecretKazikiWeaponId = "weapon_kaziki";
        public const string SecretBareHandsWeaponId = "weapon_kobushi";
        public const string SecretPikopikoMaterialId = "ore_evasion";
        public const string SecretKazikiMaterialId = "ore_attack";
        private static readonly CraftLiveSlotId[] BaseStatSlots =
        {
            CraftLiveSlotId.Top,
            CraftLiveSlotId.Right,
            CraftLiveSlotId.Left,
            CraftLiveSlotId.Bottom
        };

        public static CraftLiveStats CalculateStats(
            CraftLiveRoomState state,
            CraftLiveCatalog catalog,
            CraftLiveRules rules,
            float mixingBonus)
        {
            CraftLiveStats stats = new CraftLiveStats();
            if (state != null && catalog != null)
            {
                CraftLiveWeaponDefinition weapon =
                    catalog.FindWeapon(state.selectedWeaponId);
                if (weapon != null)
                {
                    stats.Add(weapon.BaseStats);
                }

                foreach (CraftLiveSlotId slot in BaseStatSlots)
                {
                    AddBaseMaterial(state, catalog, slot, ref stats);
                }
            }

            stats.AddAll(mixingBonus);
            return rules != null
                ? stats.Clamp(rules.MaximumStat)
                : stats.Clamp(100f);
        }

        public static string DetermineBuildType(CraftLiveStats stats)
        {
            if (stats.attackRate >= 30f &&
                stats.defenseRate >= 30f &&
                stats.evasionRate >= 30f)
            {
                return "万能型";
            }

            if (stats.attackRate >= stats.defenseRate &&
                stats.attackRate >= stats.evasionRate &&
                stats.attackRate >= 30f)
            {
                return "攻撃型";
            }

            if (stats.defenseRate >= stats.evasionRate &&
                stats.defenseRate >= 30f)
            {
                return "防御型";
            }

            if (stats.evasionRate >= 30f)
            {
                return "回避型";
            }

            return "バランス型";
        }

        public static string ValidateSynthesis(
            CraftLiveRoomState state,
            CraftLiveCatalog catalog)
        {
            return ValidateSynthesis(state, catalog, null);
        }

        public static string ValidateSynthesis(
            CraftLiveRoomState state,
            CraftLiveCatalog catalog,
            CraftLiveRules rules)
        {
            if (state == null)
            {
                return "RoomStateがありません。";
            }

            if (catalog == null)
            {
                return "Catalogが設定されていません。";
            }

            if (!state.weaponSelectionConfirmed ||
                catalog.FindWeapon(state.selectedWeaponId) == null)
            {
                return "合成する武器を選択して確定してください。";
            }

            if (state.placement.status !=
                    CraftLivePlacementStatus.Idle ||
                (state.transferQueue != null &&
                 state.transferQueue.Count > 0))
            {
                return "転送待ちまたは転送中の素材があります。";
            }

            bool requireAttribute =
                rules == null || rules.RequireAttributeSlot;
            bool requireSkill = rules == null || rules.RequireSkillSlot;

            string error = ValidateMaterialSlot(
                state,
                catalog,
                CraftLiveSlotId.Attribute,
                requireAttribute);
            if (!string.IsNullOrEmpty(error))
            {
                return error;
            }

            error = ValidateMaterialSlot(
                state,
                catalog,
                CraftLiveSlotId.Skill,
                requireSkill);
            if (!string.IsNullOrEmpty(error))
            {
                return error;
            }

            bool requireAllBaseSlots =
                rules != null && rules.RequireAllFourBaseSlots;
            foreach (CraftLiveSlotId slot in BaseStatSlots)
            {
                error = ValidateMaterialSlot(
                    state,
                    catalog,
                    slot,
                    requireAllBaseSlots);
                if (!string.IsNullOrEmpty(error))
                {
                    return error;
                }
            }

            return string.Empty;
        }

        public static CraftLiveResultState BuildResult(
            CraftLiveRoomState state,
            CraftLiveCatalog catalog,
            CraftLiveRules rules,
            long completedAtUnixMs)
        {
            CraftLiveWeaponDefinition weapon =
                ResolveResultWeapon(state, catalog);
            CraftLiveMaterialDefinition attribute =
                catalog.FindMaterial(state.slots.attribute);
            CraftLiveMaterialDefinition skill =
                catalog.FindMaterial(state.slots.skill);
            CraftLiveRank rank = rules != null
                ? rules.EvaluateRank(state.craft.mixPower)
                : new CraftLiveRank("通常成功", 0f);

            string weaponName = weapon != null
                ? weapon.DisplayName
                : string.Empty;
            bool secretWeapon = weapon != null &&
                IsSecretWeaponId(weapon.WeaponId);
            if (attribute != null &&
                !string.IsNullOrWhiteSpace(attribute.AttributeDisplayName))
            {
                weaponName =
                    $"{attribute.AttributeDisplayName}の{weaponName}";
            }

            return new CraftLiveResultState
            {
                weaponName = weaponName,
                weaponId = weapon != null
                    ? weapon.WeaponId
                    : string.Empty,
                weaponType = weapon != null
                    ? weapon.WeaponType
                    : CraftLiveWeaponType.Sword,
                attributeId = attribute != null
                    ? attribute.AttributeId
                    : string.Empty,
                attributeName = attribute != null
                    ? attribute.AttributeDisplayName
                    : string.Empty,
                elementEffect = attribute != null
                    ? attribute.ElementEffect
                    : new CraftLiveElementEffect(),
                skillId = skill != null
                    ? skill.SkillId
                    : string.Empty,
                skillName = skill != null
                    ? skill.SkillDisplayName
                    : string.Empty,
                skillDescription = skill != null
                    ? skill.SkillDescription
                    : string.Empty,
                skillEffect = skill != null
                    ? skill.SkillEffect
                    : new CraftLiveSkillEffect(),
                stats = secretWeapon
                    ? weapon.BaseStats.Clamp(
                        rules != null ? rules.MaximumStat : 100f)
                    : CalculateStats(
                        StateWithWeapon(state, weapon),
                        catalog,
                        rules,
                        rank.Bonus),
                attackMaterialCount = secretWeapon
                    ? 0
                    : CountPlacedUpgrade(state, "ore_attack"),
                defenseMaterialCount = secretWeapon
                    ? 0
                    : CountPlacedUpgrade(state, "ore_defence"),
                evasionMaterialCount = secretWeapon
                    ? 0
                    : CountPlacedUpgrade(state, "ore_evasion"),
                rank = rank.Name,
                completedAtUnixMs = completedAtUnixMs,
                resultSerial = GetNextResultSerial(state)
            };
        }

        public static bool IsSecretWeaponId(string weaponId)
        {
            return weaponId == SecretPikopikoWeaponId ||
                   weaponId == SecretKazikiWeaponId ||
                   weaponId == SecretBareHandsWeaponId;
        }

        private static int CountPlacedUpgrade(
            CraftLiveRoomState state,
            string materialId)
        {
            if (state == null || state.slots == null)
            {
                return 0;
            }

            int count = 0;
            foreach (CraftLiveSlotId slot in BaseStatSlots)
            {
                if (state.slots.Get(slot) == materialId)
                {
                    count++;
                }
            }

            return count;
        }

        public static CraftLiveWeaponDefinition ResolveResultWeapon(
            CraftLiveRoomState state,
            CraftLiveCatalog catalog)
        {
            if (state == null || catalog == null)
            {
                return null;
            }

            CraftLiveWeaponDefinition selected =
                catalog.FindWeapon(state.selectedWeaponId);
            if (selected != null && !state.HasAnyPlacedMaterial())
            {
                return catalog.FindWeapon(SecretBareHandsWeaponId) ??
                       selected;
            }

            if (selected != null &&
                selected.WeaponType == CraftLiveWeaponType.Sword &&
                HasFourMatchingBaseMaterials(
                    state,
                    catalog,
                    SecretPikopikoMaterialId))
            {
                return catalog.FindWeapon(SecretPikopikoWeaponId) ??
                       selected;
            }

            if (selected != null &&
                selected.WeaponType == CraftLiveWeaponType.Thrust &&
                HasFourMatchingBaseMaterials(
                    state,
                    catalog,
                    SecretKazikiMaterialId))
            {
                return catalog.FindWeapon(SecretKazikiWeaponId) ??
                       selected;
            }

            return selected;
        }

        private static CraftLiveRoomState StateWithWeapon(
            CraftLiveRoomState source,
            CraftLiveWeaponDefinition weapon)
        {
            CraftLiveRoomState copy = source.Clone();
            copy.selectedWeaponId = weapon != null
                ? weapon.WeaponId
                : source.selectedWeaponId;
            return copy;
        }

        private static bool HasFourMatchingBaseMaterials(
            CraftLiveRoomState state,
            CraftLiveCatalog catalog,
            string requiredMaterialId)
        {
            int matchingCount = 0;
            foreach (CraftLiveSlotId slot in BaseStatSlots)
            {
                string materialId = state.slots.Get(slot);
                if (string.IsNullOrWhiteSpace(materialId))
                {
                    return false;
                }

                CraftLiveMaterialDefinition material =
                    catalog.FindMaterial(materialId);
                if (material == null ||
                    material.Category != CraftLiveMaterialCategory.Upgrade ||
                    material.MaterialId != requiredMaterialId)
                {
                    return false;
                }

                matchingCount++;
            }

            // The secret recipe is exactly four matching base materials.
            // Attribute and skill slots intentionally do not participate.
            return matchingCount == BaseStatSlots.Length;
        }

        private static int GetNextResultSerial(
            CraftLiveRoomState state)
        {
            int highest = state != null &&
                          state.result != null
                ? state.result.resultSerial
                : 0;
            if (state != null &&
                state.completedWeapons != null)
            {
                foreach (CraftLiveResultState completed in
                         state.completedWeapons)
                {
                    if (completed != null)
                    {
                        highest = System.Math.Max(
                            highest,
                            completed.resultSerial);
                    }
                }
            }

            return highest + 1;
        }

        private static string ValidateMaterialSlot(
            CraftLiveRoomState state,
            CraftLiveCatalog catalog,
            CraftLiveSlotId slot,
            bool required)
        {
            string materialId = state.slots.Get(slot);
            if (string.IsNullOrWhiteSpace(materialId))
            {
                if (!required)
                {
                    return string.Empty;
                }

                if (slot == CraftLiveSlotId.Attribute)
                {
                    return "属性素材が必要です。";
                }

                if (slot == CraftLiveSlotId.Skill)
                {
                    return "固有スキル素材が必要です。";
                }

                return "4つの基礎素材枠をすべて埋めてください。";
            }

            CraftLiveMaterialDefinition material =
                catalog.FindMaterial(materialId);
            if (material == null)
            {
                return $"{CraftLiveSlot.ToKey(slot)}枠の素材が見つかりません。";
            }

            if (!material.CanUseIn(slot))
            {
                return $"{material.DisplayName}は{CraftLiveSlot.ToKey(slot)}枠に配置できません。";
            }

            return string.Empty;
        }

        private static void AddBaseMaterial(
            CraftLiveRoomState state,
            CraftLiveCatalog catalog,
            CraftLiveSlotId slot,
            ref CraftLiveStats stats)
        {
            CraftLiveMaterialDefinition material =
                catalog.FindMaterial(state.slots.Get(slot));
            if (material == null ||
                material.Category != CraftLiveMaterialCategory.Upgrade)
            {
                return;
            }

            stats.Add(material.StatModifiers);
        }
    }
}
