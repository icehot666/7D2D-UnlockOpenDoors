using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

/// <summary>
/// Makes locked-open, unowned POI doors usable in 7 Days to Die V3.x.
///
/// V3.4 compatibility candidate: resolves patch targets at runtime, so a change
/// to one game method's signature cannot prevent the other hooks from loading.
/// The game DLL and in-game behaviour still need to be validated on V3.4.
/// </summary>
public class UnlockOpenDoors : IModApi
{
    private const string Tag = "[UnlockOpenDoors] ";
    private const BindingFlags InstanceFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticFlags =
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static Type compositeType;
    private static Type doorType;
    private static Type lockType;

    private static PropertyInfo doorParentProperty;
    private static FieldInfo doorParentField;
    private static PropertyInfo lockParentProperty;
    private static FieldInfo lockParentField;

    private static MethodInfo getDoorFeature;
    private static MethodInfo getLockFeature;
    private static MethodInfo getFeatureByType;
    private static MethodInfo isOpenMethod;
    private static PropertyInfo isOpenProperty;
    private static MethodInfo isLockedMethod;
    private static PropertyInfo isLockedProperty;
    private static MethodInfo getOwnerMethod;
    private static PropertyInfo ownerProperty;
    private static MethodInfo setLockedMethod;

    private static int runtimeErrorCount;
    private static int unlockedDoorCount;

    public void InitMod(Mod _modInstance)
    {
        Debug.Log(Tag + "Initialising V3.x adaptive patch (V3.4 compatibility candidate)");

        try
        {
            compositeType = AccessTools.TypeByName("TileEntityComposite");
            doorType = AccessTools.TypeByName("TEFeatureDoor");
            lockType = AccessTools.TypeByName("TEFeatureLockable");

            if (compositeType == null || doorType == null || lockType == null)
            {
                Debug.LogError(Tag + "Game door types missing: composite=" +
                    (compositeType != null) + ", door=" + (doorType != null) +
                    ", lock=" + (lockType != null) +
                    ". Please provide the V3.4 Assembly-CSharp.dll or output log.");
                return;
            }

            ConfigureAccessors();

            var harmony = new Harmony("icehot666.7D2D.UnlockOpenDoors");
            MethodInfo lockPrefix = typeof(UnlockOpenDoors).GetMethod(
                "SetLockedPrefix", StaticFlags);
            MethodInfo openPostfix = typeof(UnlockOpenDoors).GetMethod(
                "SetOpenPostfix", StaticFlags);
            MethodInfo readPostfix = typeof(UnlockOpenDoors).GetMethod(
                "CompositeReadPostfix", StaticFlags);

            int lockHooks = PatchNamed(harmony, lockType, "SetLocked", lockPrefix,
                null, true);
            int openHooks = PatchNamed(harmony, doorType, "SetOpen", null,
                openPostfix, false);
            int readHooks = PatchNamed(harmony, compositeType, "read", null,
                readPostfix, false);

            Debug.Log(Tag + "Installed hooks: SetLocked=" + lockHooks +
                ", SetOpen=" + openHooks + ", composite read=" + readHooks +
                ". A nonzero count for each is expected on V3.0.");

            if (lockHooks == 0 || openHooks == 0 || readHooks == 0)
            {
                Debug.LogWarning(Tag + "One or more hook targets were not found. " +
                    "Check the game output log for method signatures.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError(Tag + "Initialisation failed: " + ex);
        }
    }

    private static void ConfigureAccessors()
    {
        doorParentProperty = doorType.GetProperty("Parent", InstanceFlags);
        doorParentField = doorType.GetField("Parent", InstanceFlags);
        lockParentProperty = lockType.GetProperty("Parent", InstanceFlags);
        lockParentField = lockType.GetField("Parent", InstanceFlags);

        MethodInfo genericFeature = compositeType.GetMethods(InstanceFlags)
            .FirstOrDefault(m => m.Name == "GetFeature" &&
                m.IsGenericMethodDefinition &&
                m.GetGenericArguments().Length == 1 &&
                m.GetParameters().Length == 0);

        if (genericFeature != null)
        {
            getDoorFeature = genericFeature.MakeGenericMethod(doorType);
            getLockFeature = genericFeature.MakeGenericMethod(lockType);
        }
        else
        {
            getFeatureByType = compositeType.GetMethods(InstanceFlags)
                .FirstOrDefault(m => m.Name == "GetFeature" &&
                    m.GetParameters().Length == 1 &&
                    m.GetParameters()[0].ParameterType == typeof(Type));
        }

        isOpenMethod = NoArgMethod(doorType, "IsOpen");
        isOpenProperty = doorType.GetProperty("IsOpen", InstanceFlags);
        isLockedMethod = NoArgMethod(lockType, "IsLocked");
        isLockedProperty = lockType.GetProperty("IsLocked", InstanceFlags);
        getOwnerMethod = NoArgMethod(lockType, "GetOwner");
        ownerProperty = lockType.GetProperty("Owner", InstanceFlags);

        // Prefer the known V3 overload: SetLocked(bool). If a later game build
        // adds optional parameters, they can be supplied using their defaults.
        setLockedMethod = lockType.GetMethods(InstanceFlags)
            .Where(m => m.Name == "SetLocked" && !m.IsStatic &&
                m.GetParameters().Length > 0 &&
                m.GetParameters()[0].ParameterType == typeof(bool) &&
                m.GetParameters().Skip(1).All(p => p.IsOptional))
            .OrderBy(m => m.GetParameters().Length)
            .FirstOrDefault();

        Debug.Log(Tag + "Accessors: door Parent=" +
            (doorParentProperty != null || doorParentField != null) +
            ", lock Parent=" + (lockParentProperty != null || lockParentField != null));

        if ((getDoorFeature == null && getFeatureByType == null) ||
            (getOwnerMethod == null && ownerProperty == null) ||
            (isOpenMethod == null && isOpenProperty == null) ||
            (isLockedMethod == null && isLockedProperty == null) ||
            setLockedMethod == null)
        {
            Debug.LogWarning(Tag + "One or more feature accessors are missing. " +
                "The patch will fail safely rather than unlock owned doors. " +
                "GetFeature=" + (getDoorFeature != null || getFeatureByType != null) +
                ", GetOwner=" + (getOwnerMethod != null || ownerProperty != null) +
                ", IsOpen=" + (isOpenMethod != null || isOpenProperty != null) +
                ", IsLocked=" + (isLockedMethod != null || isLockedProperty != null) +
                ", SetLocked=" + (setLockedMethod != null));
        }
    }

    private static MethodInfo NoArgMethod(Type type, string name)
    {
        return type.GetMethod(name, InstanceFlags, null, Type.EmptyTypes, null);
    }

    private static int PatchNamed(Harmony harmony, Type type, string methodName,
        MethodInfo prefix, MethodInfo postfix, bool requireFirstBool)
    {
        int installed = 0;
        MethodInfo[] methods = type.GetMethods(InstanceFlags)
            .Where(m => m.DeclaringType == type && !m.IsStatic &&
                !m.IsAbstract && !m.ContainsGenericParameters &&
                string.Equals(m.Name, methodName, StringComparison.OrdinalIgnoreCase) &&
                (!requireFirstBool || (m.GetParameters().Length > 0 &&
                  m.GetParameters()[0].ParameterType == typeof(bool))))
            .ToArray();

        if (methods.Length == 0)
        {
            string[] nearMatches = type.GetMethods(InstanceFlags)
                .Where(m => m.Name.IndexOf(methodName,
                    StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(12)
                .Select(m => m.ToString())
                .ToArray();
            Debug.LogWarning(Tag + "No matching " + type.Name + "." +
                methodName + " overloads found. Candidates: " +
                (nearMatches.Length == 0 ? "none" :
                    string.Join(" | ", nearMatches)));
        }

        foreach (MethodInfo method in methods)
        {
            try
            {
                harmony.Patch(method,
                    prefix == null ? null : new HarmonyMethod(prefix),
                    postfix == null ? null : new HarmonyMethod(postfix));
                installed++;
                Debug.Log(Tag + "Patched " + type.Name + "." +
                    method.Name + "(" + string.Join(", ", method.GetParameters()
                        .Select(p => p.ParameterType.Name).ToArray()) + ")");
            }
            catch (Exception ex)
            {
                Debug.LogError(Tag + "Could not patch " + method + ": " + ex);
            }
        }

        return installed;
    }

    private static object GetParent(object feature, PropertyInfo property, FieldInfo field)
    {
        if (feature == null) return null;
        if (property != null) return property.GetValue(feature, null);
        return field == null ? null : field.GetValue(feature);
    }

    private static object GetFeature(object composite, bool wantDoor)
    {
        if (composite == null) return null;

        MethodInfo generic = wantDoor ? getDoorFeature : getLockFeature;
        if (generic != null) return generic.Invoke(composite, null);
        if (getFeatureByType != null)
            return getFeatureByType.Invoke(composite,
                new object[] { wantDoor ? doorType : lockType });

        return null;
    }

    private static bool ReadBool(object feature, MethodInfo method, PropertyInfo property)
    {
        if (feature == null) return false;
        if (method != null) return method.Invoke(feature, null) is bool value && value;
        return property != null && property.GetValue(feature, null) is bool value2 && value2;
    }

    // An unknown ownership API is deliberately treated as NOT safe to unlock.
    private static bool IsUnowned(object lockable)
    {
        if (lockable == null || (getOwnerMethod == null && ownerProperty == null))
            return false;

        object owner = getOwnerMethod != null
            ? getOwnerMethod.Invoke(lockable, null)
            : ownerProperty.GetValue(lockable, null);
        return owner == null;
    }

    private static void SetUnlocked(object lockable)
    {
        if (setLockedMethod == null) return;

        ParameterInfo[] parameters = setLockedMethod.GetParameters();
        var arguments = new object[parameters.Length];
        arguments[0] = false;
        for (int i = 1; i < parameters.Length; i++)
            arguments[i] = parameters[i].DefaultValue;

        setLockedMethod.Invoke(lockable, arguments);
        int count = System.Threading.Interlocked.Increment(ref unlockedDoorCount);
        if (count <= 5)
            Debug.Log(Tag + "Unlocked open, unowned POI door (sample " + count + "/5).");
    }

    private static bool IsOpenUnownedLock(object lockable)
    {
        object composite = GetParent(lockable, lockParentProperty, lockParentField);
        if (composite == null || !IsUnowned(lockable)) return false;
        return ReadBool(GetFeature(composite, true), isOpenMethod, isOpenProperty);
    }

    private static void UnlockOpenUnownedDoor(object composite)
    {
        if (composite == null) return;
        object door = GetFeature(composite, true);
        object lockable = GetFeature(composite, false);

        if (door != null && lockable != null &&
            ReadBool(door, isOpenMethod, isOpenProperty) &&
            ReadBool(lockable, isLockedMethod, isLockedProperty) &&
            IsUnowned(lockable))
        {
            SetUnlocked(lockable);
        }
    }

    // Parameter index __0 is Harmony's name for the original first argument.
    private static void SetLockedPrefix(object __instance, ref bool __0)
    {
        if (!__0) return;
        try
        {
            if (IsOpenUnownedLock(__instance)) __0 = false;
        }
        catch (Exception ex) { LogRuntimeError("SetLockedPrefix", ex); }
    }

    private static void SetOpenPostfix(object __instance)
    {
        try
        {
            UnlockOpenUnownedDoor(GetParent(__instance,
                doorParentProperty, doorParentField));
        }
        catch (Exception ex) { LogRuntimeError("SetOpenPostfix", ex); }
    }

    private static void CompositeReadPostfix(object __instance)
    {
        try { UnlockOpenUnownedDoor(__instance); }
        catch (Exception ex) { LogRuntimeError("CompositeReadPostfix", ex); }
    }

    // Avoid filling the console every frame if a future patch breaks an accessor.
    private static void LogRuntimeError(string location, Exception ex)
    {
        if (System.Threading.Interlocked.Increment(ref runtimeErrorCount) <= 5)
            Debug.LogError(Tag + location + ": " + ex);
    }
}
