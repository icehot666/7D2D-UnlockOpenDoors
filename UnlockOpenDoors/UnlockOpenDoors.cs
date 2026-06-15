using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
using static TileEntity;

/// <summary>
/// Makes locked-open POI doors, hatches, and gates usable.
/// Updated for the 7 Days to Die V3 composite tile-entity/feature system.
/// </summary>
public class UnlockOpenDoors : IModApi
{
    /// <summary>
    /// Mod initialization.
    /// </summary>
    public void InitMod(Mod _modInstance)
    {
        Debug.Log("Loading mod: " + GetType());

        new Harmony(GetType().FullName)
            .PatchAll(Assembly.GetExecutingAssembly());
    }

    /// <summary>
    /// Returns true when this lock belongs to an unowned door that is open.
    /// </summary>
    private static bool IsOpenUnownedDoor(
        TEFeatureLockable lockable)
    {
        if (lockable == null ||
            lockable.Parent == null ||
            lockable.GetOwner() != null)
        {
            return false;
        }

        TEFeatureDoor door =
            lockable.Parent.GetFeature<TEFeatureDoor>();

        return door != null && door.IsOpen();
    }

    /// <summary>
    /// Unlocks a composite door when it is open, locked,
    /// and not player-owned.
    /// </summary>
    private static void UnlockOpenUnownedDoor(
        TileEntityComposite tileEntity)
    {
        if (tileEntity == null)
        {
            return;
        }

        TEFeatureDoor door =
            tileEntity.GetFeature<TEFeatureDoor>();

        TEFeatureLockable lockable =
            tileEntity.GetFeature<TEFeatureLockable>();

        if (door != null &&
            lockable != null &&
            door.IsOpen() &&
            lockable.IsLocked() &&
            lockable.GetOwner() == null)
        {
            lockable.SetLocked(false);
        }
    }

    /// <summary>
    /// Prevents an unowned open door from being locked.
    /// </summary>
    [HarmonyPatch(
        typeof(TEFeatureLockable),
        nameof(TEFeatureLockable.SetLocked))]
    public static class TEFeatureLockable_SetLocked
    {
        public static void Prefix(
            TEFeatureLockable __instance,
            ref bool __0)
        {
            if (__0 && IsOpenUnownedDoor(__instance))
            {
                __0 = false;
            }
        }
    }

    /// <summary>
    /// Unlocks an unowned POI door immediately after
    /// the game opens it.
    /// </summary>
    [HarmonyPatch(
        typeof(TEFeatureDoor),
        nameof(TEFeatureDoor.SetOpen))]
    public static class TEFeatureDoor_SetOpen
    {
        public static void Postfix(
            TEFeatureDoor __instance)
        {
            if (__instance != null &&
                __instance.Parent != null)
            {
                UnlockOpenUnownedDoor(__instance.Parent);
            }
        }
    }

    /// <summary>
    /// Repairs open-and-locked doors loaded from save
    /// or prefab data.
    /// </summary>
    [HarmonyPatch(
        typeof(TileEntityComposite),
        "read",
        new Type[]
        {
            typeof(PooledBinaryReader),
            typeof(StreamModeRead),
            typeof(int[])
        })]
    public static class TileEntityComposite_Read
    {
        public static void Postfix(
            TileEntityComposite __instance)
        {
            UnlockOpenUnownedDoor(__instance);
        }
    }
}
