UnlockOpenDoors - 7 Days to Die V3.4 EXP compatibility
======================================================

Status: Tested in v3.4.0 EXP release only.
Based on the previously working V3.0 C# implementation.

What changed
------------
- Preserves original safety rule: only UNOWNED, OPEN, LOCKED composite POI doors are unlocked.
- Reflectively locates TEFeatureDoor / TEFeatureLockable / TileEntityComposite at runtime.
- Scans current game class methods rather than assuming the old three-argument
  TileEntityComposite.read signature still exists.
- Patches methods independently, so a missing overload does not abort all patches.
- Prints installed hook counts, method signatures, and first five successful unlocks.
- If ownership/accessor detection fails, does not unlock doors indiscriminately.

Installation / build
--------------------
1. Extract this ZIP over the ROOT of your existing GitHub checkout:
      7D2D-UnlockOpenDoors/
        UnlockOpenDoors.sln
        UnlockOpenDoors/UnlockOpenDoors.cs  <-- replaced by this kit
        Build.ps1                           <-- added by this kit
   Preserve the other repository files including .csproj, AssemblyInfo.cs and ModInfo.xml.

2. Close the game; in PowerShell from the repository root:
      .\Build.ps1
   Or specify a different Steam game location:
      .\Build.ps1 -GameDir 'D:\SteamLibrary\steamapps\common\7 Days To Die'

3. The build uses your *current* game Assembly-CSharp.dll and Harmony DLL.
   It should output Releases\UnlockOpenDoors\UnlockOpenDoors.dll.

4. Copy that DLL into your EXISTING installed UnlockOpenDoors mod folder,
   replacing only the DLL. Or use .\Build.ps1 -Install to do this automatically
   for existing Mods\UnlockOpenDoors folders with ModInfo.xml.
   NOTE: The existing .csproj may also auto-copy a built DLL to an existing
   GameDir\Mods\UnlockOpenDoors folder as part of its build target.

5. Start V3.4 EXP with Easy Anti-Cheat disabled and load a POI with a door
   that is visually OPEN but shows as LOCKED. Check if it can be closed.
   Check player-owned/claimed doors still behave normally.

6. Open the latest game's output_log_client__*.txt under
      %APPDATA%\7DaysToDie\logs\
   and search for [UnlockOpenDoors]. Expected log has "Installed hooks" with
   non-zero numbers for SetLocked, SetOpen and composite read.
   A first successful unlock logs "Unlocked open, unowned POI door".

If this still does not work, send back the V3.4 client log or, ideally, the
V3.4 7DaysToDie_Data\Managed\Assembly-CSharp.dll and the log. The actual
method/field changes can then be inspected and a proper targeted fix built.

IMPORTANT: This is an UNVERIFIED compatibility patch, not a confirmed V3.4 fix.
Do not publish it as tested until you have verified the behaviour in game.
