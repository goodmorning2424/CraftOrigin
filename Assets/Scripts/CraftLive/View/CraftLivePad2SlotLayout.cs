using System;
using System.Collections.Generic;
using UnityEngine;

namespace CraftOrigin.CraftLive
{
    public enum CraftLivePad2PhysicalSlot
    {
        UpperLeft,
        MiddleLeft,
        UpperRight,
        MiddleRight,
        LowerLeft,
        LowerRight
    }

    [Serializable]
    public readonly struct CraftLivePad2SlotSpec
    {
        public CraftLivePad2SlotSpec(
            CraftLivePad2PhysicalSlot physicalSlot,
            CraftLiveSlotId slotId,
            string label,
            Vector3 defaultPosition,
            Vector3 flowEndPosition)
        {
            PhysicalSlot = physicalSlot;
            SlotId = slotId;
            Label = label;
            DefaultPosition = defaultPosition;
            FlowEndPosition = flowEndPosition;
        }

        public CraftLivePad2PhysicalSlot PhysicalSlot { get; }
        public CraftLiveSlotId SlotId { get; }
        public string Label { get; }
        public Vector3 DefaultPosition { get; }
        public Vector3 FlowEndPosition { get; }
    }

    public static class CraftLivePad2SlotLayout
    {
        private static readonly CraftLivePad2SlotSpec[] Specs =
        {
            new CraftLivePad2SlotSpec(
                CraftLivePad2PhysicalSlot.UpperLeft,
                CraftLiveSlotId.Top,
                "基礎",
                new Vector3(-1.47f, 2.12f, 0f),
                new Vector3(-0.66f, 1.8f, 0f)),
            new CraftLivePad2SlotSpec(
                CraftLivePad2PhysicalSlot.MiddleLeft,
                CraftLiveSlotId.Left,
                "基礎",
                new Vector3(-1.47f, 0f, 0f),
                new Vector3(-0.66f, 0.67f, 0f)),
            new CraftLivePad2SlotSpec(
                CraftLivePad2PhysicalSlot.UpperRight,
                CraftLiveSlotId.Right,
                "基礎",
                new Vector3(1.47f, 2.12f, 0f),
                new Vector3(0.66f, 1.8f, 0f)),
            new CraftLivePad2SlotSpec(
                CraftLivePad2PhysicalSlot.MiddleRight,
                CraftLiveSlotId.Bottom,
                "基礎",
                new Vector3(1.47f, 0f, 0f),
                new Vector3(0.66f, 0.67f, 0f)),
            new CraftLivePad2SlotSpec(
                CraftLivePad2PhysicalSlot.LowerLeft,
                CraftLiveSlotId.Skill,
                "スキル",
                new Vector3(-1.47f, -2.12f, 0f),
                new Vector3(-0.2f, 0.12f, 0f)),
            new CraftLivePad2SlotSpec(
                CraftLivePad2PhysicalSlot.LowerRight,
                CraftLiveSlotId.Attribute,
                "タイプ",
                new Vector3(1.47f, -2.12f, 0f),
                new Vector3(0.2f, 0.12f, 0f))
        };

        public static IReadOnlyList<CraftLivePad2SlotSpec> All =>
            Specs;

        public static CraftLivePad2SlotSpec Get(
            CraftLivePad2PhysicalSlot physicalSlot)
        {
            foreach (CraftLivePad2SlotSpec spec in Specs)
            {
                if (spec.PhysicalSlot == physicalSlot)
                {
                    return spec;
                }
            }

            throw new ArgumentOutOfRangeException(
                nameof(physicalSlot),
                physicalSlot,
                null);
        }

        public static CraftLiveSlotId GetSlotId(
            CraftLivePad2PhysicalSlot physicalSlot)
        {
            return Get(physicalSlot).SlotId;
        }

        public static CraftLivePad2SlotSpec Get(
            CraftLiveSlotId slotId)
        {
            foreach (CraftLivePad2SlotSpec spec in Specs)
            {
                if (spec.SlotId == slotId)
                {
                    return spec;
                }
            }

            throw new ArgumentOutOfRangeException(
                nameof(slotId),
                slotId,
                null);
        }
    }
}
