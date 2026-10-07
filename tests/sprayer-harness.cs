// Controlled logic harness, NOT the game runtime. Compile this file together
// with the UNMODIFIED production Scripts/*.cs and the user's REAL 0Harmony.dll.
// The doubles below deliberately expose query, cargo, buff, damage and AI calls.
// No test in this file establishes Unity scene behavior or network replication.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Itachi.DroneRadiationSprayer;
using UnityEngine;

public interface IModApi { void InitMod(Mod mod); }
public class Mod { }
public static class TagGroup { public struct Global { } }
public struct FastTags<T>
{
    private HashSet<string> values;
    public static FastTags<T> Parse(string text)
    {
        return new FastTags<T> { values = new HashSet<string>(text.Split(',')) };
    }
    public bool Matches(HashSet<string> tags)
    {
        if (values == null) return false;
        foreach (string value in values) if (tags.Contains(value)) return true;
        return false;
    }
}
[Flags] public enum EntityFlags { None = 0, Zombie = 1, Player = 2, Animal = 4 }
namespace UnityEngine
{
    public static class Time { public static float time; }
    public static class Debug
    {
        public static readonly List<string> Logs = new List<string>();
        public static readonly List<string> Warnings = new List<string>();
        public static void Log(object message) { Logs.Add(message.ToString()); }
        public static void LogWarning(object message) { Warnings.Add(message.ToString()); }
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public float sqrMagnitude { get { return x * x + y * y + z * z; } }
        public override string ToString() { return string.Format("({0}, {1}, {2})", x, y, z); }
    }
}
public class ItemClass { public string Name; }
public class ItemValue
{
    public ItemClass ItemClass;
    public ItemValue[] Mods = new ItemValue[0];
    public int ModificationCount { get { return Mods.Length; } }
    public ItemValue GetModification(int index) { return Mods[index]; }
}
public class Entity
{
    public int entityId;
    public World world;
    public Vector3 position;
    public bool IsDespawned, markedForUnload, isUnloaded, isEntityRemote, Dead;
    public int DamageCalls, AiCalls, WakeCalls;
    public virtual bool IsDead() { return Dead; }
    public virtual void DamageEntity() { DamageCalls++; }
    public virtual void SetAttackTarget() { AiCalls++; }
    public virtual void SetRevengeTarget() { AiCalls++; }
    public virtual void SetSleeperActive() { WakeCalls++; }
}
public class EntityAlive : Entity
{
    public EntityBuffs Buffs = new EntityBuffs();
    public HashSet<string> Tags = new HashSet<string>();
}
public class EntityPlayer : EntityAlive { }
public class EntityPlayerLocal : EntityPlayer { }
public class EntityAnimal : EntityAlive { }
public class EntityVulture : EntityAnimal { }
public class EntityZombie : EntityAlive
{
    public bool IsSleeping = true, ThrowOnTags;
    public EntityZombie() { Tags.Add("radiated"); }
    public bool HasAnyTags(FastTags<TagGroup.Global> query)
    {
        if (ThrowOnTags) throw new InvalidOperationException("controlled tag failure");
        return query.Matches(Tags);
    }
}
public class EntityDrone : EntityAlive
{
    public enum State { None, Idle, Sentry, Follow, Heal, Attack, Shutdown, NoClip, Teleport }
    public bool isShutdown, isShutdownPending, isBeingPickedUp;
    public int Health = 100;
    public Entity Owner;
    public ItemValue OriginalItemValue;
    public State CurrentState = State.Idle;
    public State GetState() { return CurrentState; }
    public int CargoReads, OriginalUpdates;
    public readonly List<ItemValue> StoredCargo = new List<ItemValue>();
    public List<ItemValue> inventory { get { CargoReads++; return StoredCargo; } }
    public Action OnOriginalUpdate;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public virtual void OnUpdateEntity()
    {
        OriginalUpdates++;
        if (OnOriginalUpdate != null) OnOriginalUpdate();
    }
}
public class World
{
    public bool Remote, ThrowOnQuery, ThrowOnGetEntity;
    public int QueryCount;
    public EntityFlags LastFlags;
    public Vector3 LastCenter;
    public float LastRadius;
    public Action OnQuery;
    public readonly Dictionary<int, Entity> Entities = new Dictionary<int, Entity>();
    public readonly List<Entity> QueryResults = new List<Entity>();
    public bool IsRemote() { return Remote; }
    public Entity GetEntity(int id)
    {
        if (ThrowOnGetEntity) throw new InvalidOperationException("controlled unload lookup failure");
        Entity entity;
        return Entities.TryGetValue(id, out entity) ? entity : null;
    }
    public void GetEntitiesAround(EntityFlags flags, Vector3 center, float radius, List<Entity> output)
    {
        QueryCount++;
        LastFlags = flags;
        LastCenter = center;
        LastRadius = radius;
        // Return an intentionally overbroad broad phase so production MUST do
        // its own type, eligibility and true spherical-distance checks.
        output.AddRange(QueryResults);
        if (OnQuery != null) OnQuery();
        if (ThrowOnQuery) throw new InvalidOperationException("controlled query failure after append");
    }
}
public sealed class BuffCall
{
    public string Name;
    public int Instigator;
    public bool NetSync, RequireTags;
    public float DurationOverride;
}
public static class BuffClass
{
    // Deliberately shared. Production must never alter a global buff duration.
    public static float Duration = 15f;
}
public class EntityBuffs
{
    public readonly List<BuffCall> Calls = new List<BuffCall>();
    public bool ThrowOnAdd;
    public float ExpiresAt;
    public int ActiveBuffCount;
    public void AddBuff(string name, int instigator, bool netSync, bool requireTags, float duration)
    {
        Calls.Add(new BuffCall { Name = name, Instigator = instigator, NetSync = netSync, RequireTags = requireTags, DurationOverride = duration });
        if (ThrowOnAdd) throw new InvalidOperationException("controlled AddBuff failure");
        // Controlled approximation of native replace/refresh semantics. This
        // is NOT evidence of how Unity or native 7DTD executes buff XML.
        ExpiresAt = Time.time + (duration < 0f ? BuffClass.Duration : duration);
        ActiveBuffCount = 1;
    }
}

internal static class SprayerHarness
{
    private static int passed, failed;
    private sealed class Fixture
    {
        public World World = new World();
        public EntityPlayer Owner;
        public EntityDrone Drone;
        public EntityZombie Zombie;
        public Fixture()
        {
            Owner = new EntityPlayer { entityId = 1, world = World };
            Drone = NewDrone(World, Owner, 2);
            Zombie = new EntityZombie { entityId = 3, world = World, position = new Vector3(1, 0, 0) };
            World.Entities[Owner.entityId] = Owner;
            World.Entities[Drone.entityId] = Drone;
            World.Entities[Zombie.entityId] = Zombie;
            World.QueryResults.Add(Zombie);
        }
        public void Tick(float now) { Time.time = now; SprayerController.Tick(Drone); }
    }
    private static ItemValue Attachment() { return new ItemValue { ItemClass = new ItemClass { Name = "modDroneRadiationSprayer" } }; }
    private static EntityDrone NewDrone(World world, EntityPlayer owner, int id)
    {
        return new EntityDrone { entityId = id, world = world, Owner = owner,
            OriginalItemValue = new ItemValue { Mods = new[] { Attachment() } } };
    }
    private static void Assert(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
    }
    private static void Equal<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception(label + ": expected " + expected + ", got " + actual);
    }
    private static void Test(string name, Action action)
    {
        SprayerController.Reset();
        Time.time = 0;
        Debug.Logs.Clear(); Debug.Warnings.Clear(); BuffClass.Duration = 15f;
        try { action(); Equal(15f, BuffClass.Duration, "shared BuffClass.Duration unchanged"); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + " :: " + ex); }
    }
    private static void Gate(string name, Action<Fixture> change)
    {
        Test(name, delegate { var f = new Fixture(); change(f); f.Tick(0); Equal(0, f.World.QueryCount, "no query"); Equal(0, f.Zombie.Buffs.Calls.Count, "no buff"); Equal(0, f.Drone == null ? 0 : f.Drone.CargoReads, "no cargo read"); });
    }
    private static void TargetGate(string name, Action<Fixture> change)
    {
        Test(name, delegate { var f = new Fixture(); change(f); f.Tick(0); Equal(1, f.World.QueryCount, "one broad-phase query"); if (f.Zombie.Buffs != null) Equal(0, f.Zombie.Buffs.Calls.Count, "target excluded"); AssertCandidatesCleared(f.Drone); });
    }
    private static void Geometry(string name, Vector3 center, Vector3 target, bool included)
    {
        Test(name, delegate { var f = new Fixture(); f.Drone.position = center; f.Zombie.position = target; f.Tick(0); Equal(included ? 1 : 0, f.Zombie.Buffs.Calls.Count, "sphere decision"); });
    }
    private static void AssertCandidatesCleared(EntityDrone drone)
    {
        object table = typeof(SprayerController).GetField("drones", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        object[] args = new object[] { drone, null };
        bool exists = (bool)table.GetType().GetMethod("TryGetValue").Invoke(table, args);
        Assert(exists, "drone state exists");
        IList list = (IList)args[1].GetType().GetField("Candidates", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(args[1]);
        Equal(0, list.Count, "candidate references cleared");
    }
    private static void NativeArgs(EntityZombie zombie)
    {
        Assert(zombie.Buffs.Calls.Count > 0, "buff call recorded");
        foreach (BuffCall call in zombie.Buffs.Calls)
        {
            Equal("buffRadiatedRegenBlock15", call.Name, "native buff name");
            Equal(-1, call.Instigator, "native instigator");
            Equal(true, call.NetSync, "native netSync");
            Equal(false, call.RequireTags, "native requireTags");
            Equal(-1f, call.DurationOverride, "native duration override");
        }
    }
    public static int Main()
    {
        Console.WriteLine("DroneRadiationSprayer CONTROLLED HARNESS");
        Console.WriteLine("Production sources compiled unchanged into this executable; game and Unity types are controlled doubles.");
        Console.WriteLine("Real Harmony assembly: " + typeof(Harmony).Assembly.FullName);
        Console.WriteLine("Real Harmony location: " + typeof(Harmony).Assembly.Location);
        Console.WriteLine("UTC run: " + DateTime.UtcNow.ToString("o"));
        Console.WriteLine("NOT an in-game, Unity, save/load, F10 integration, or network replication test.\n");

        Geometry("geometry/origin", new Vector3(), new Vector3(), true);
        Geometry("geometry/exact +X 25m boundary", new Vector3(), new Vector3(25, 0, 0), true);
        Geometry("geometry/+X 0.001m inside", new Vector3(), new Vector3(24.999f, 0, 0), true);
        Geometry("geometry/+X 0.001m outside", new Vector3(), new Vector3(25.001f, 0, 0), false);
        Geometry("geometry/exact -X 25m boundary", new Vector3(), new Vector3(-25, 0, 0), true);
        Geometry("geometry/negative coordinates boundary", new Vector3(-100, -50, -200), new Vector3(-125, -50, -200), true);
        Geometry("geometry/negative coordinates outside", new Vector3(-100, -50, -200), new Vector3(-125.001f, -50, -200), false);
        Geometry("geometry/vertical +Y boundary", new Vector3(), new Vector3(0, 25, 0), true);
        Geometry("geometry/vertical -Y boundary", new Vector3(), new Vector3(0, -25, 0), true);
        Geometry("geometry/vertical outside", new Vector3(), new Vector3(0, 25.001f, 0), false);
        Geometry("geometry/+Z boundary", new Vector3(), new Vector3(0, 0, 25), true);
        Geometry("geometry/3-4-5 diagonal boundary", new Vector3(), new Vector3(15, 20, 0), true);
        Geometry("geometry/full sphere diagonal inside", new Vector3(), new Vector3(14, 14, 14), true);
        Geometry("geometry/box corner excluded", new Vector3(), new Vector3(25, 25, 25), false);
        Geometry("geometry/horizontal corner excluded", new Vector3(), new Vector3(20, 0, 20), false);
        Geometry("geometry/NaN position excluded", new Vector3(), new Vector3(float.NaN, 0, 0), false);
        Geometry("geometry/infinite position excluded", new Vector3(), new Vector3(float.PositiveInfinity, 0, 0), false);

        Test("attachment/installed exact native buff arguments", delegate { var f = new Fixture(); f.Tick(0); NativeArgs(f.Zombie); Equal(0, f.Drone.CargoReads, "cargo reads"); });
        Test("attachment/sparse slots and null ItemClass", delegate { var f = new Fixture(); f.Drone.OriginalItemValue.Mods = new[] { null, new ItemValue(), null, Attachment(), null }; f.Tick(0); Equal(1, f.Zombie.Buffs.Calls.Count, "installed late slot found"); Equal(0, f.Drone.CargoReads, "cargo reads"); });
        Gate("attachment/null OriginalItemValue", f => f.Drone.OriginalItemValue = null);
        Gate("attachment/empty modification slots", f => f.Drone.OriginalItemValue.Mods = new ItemValue[0]);
        Gate("attachment/all null slots", f => f.Drone.OriginalItemValue.Mods = new ItemValue[] { null, null });
        Gate("attachment/null ItemClass ignored", f => f.Drone.OriginalItemValue.Mods = new[] { new ItemValue() });
        Gate("attachment/different attachment ignored", f => f.Drone.OriginalItemValue.Mods[0].ItemClass.Name = "modDroneArmorPlating");
        Gate("attachment/name match is exact", f => f.Drone.OriginalItemValue.Mods[0].ItemClass.Name = "MODDRONERADIATIONSPRAYER");
        Gate("attachment/cargo does not activate and is never read", delegate(Fixture f) { f.Drone.OriginalItemValue.Mods = new ItemValue[0]; f.Drone.StoredCargo.Add(Attachment()); });
        Test("attachment/removal stops subsequent pulse", delegate { var f = new Fixture(); f.Tick(0); f.Drone.OriginalItemValue.Mods = new ItemValue[0]; f.Tick(1); f.Tick(2); Equal(1, f.World.QueryCount, "queries stop after removal"); Equal(1, f.Zombie.Buffs.Calls.Count, "no buff after removal"); });
        Test("attachment/reinstallation resumes at next pulse", delegate { var f = new Fixture(); f.Tick(0); f.Drone.OriginalItemValue.Mods = new ItemValue[0]; f.Tick(1); f.Drone.OriginalItemValue.Mods = new[] { Attachment() }; f.Tick(2); Equal(2, f.Zombie.Buffs.Calls.Count, "buff resumes"); });
        Test("attachment/another drone cannot activate unequipped drone", delegate { var f = new Fixture(); EntityDrone other = NewDrone(f.World, f.Owner, 20); f.World.Entities[20] = other; f.Drone.OriginalItemValue.Mods = new ItemValue[0]; f.Tick(0); Equal(0, f.World.QueryCount, "unequipped no query"); SprayerController.Tick(other); Equal(1, f.Zombie.Buffs.Calls.Count, "equipped other activates"); Equal(0, other.CargoReads + f.Drone.CargoReads, "cargo reads"); });

        Gate("authority/client remote world excluded", f => f.World.Remote = true);
        Test("authority/server accepts remotely owned drone", delegate { var f = new Fixture(); f.Drone.isEntityRemote = true; f.Tick(0); Equal(1, f.Zombie.Buffs.Calls.Count, "server-owned authority despite isEntityRemote"); });
        Test("owner/multiplayer EntityPlayer owner accepted", delegate { var f = new Fixture(); Assert(!(f.Owner is EntityPlayerLocal), "owner is not EntityPlayerLocal"); f.Tick(0); Equal(1, f.Zombie.Buffs.Calls.Count, "remote player owner works"); });
        Gate("owner/null owner", f => f.Drone.Owner = null);
        Gate("owner/non-player owner", f => f.Drone.Owner = new EntityAnimal { world = f.World });
        Gate("owner/dead owner", f => f.Owner.Dead = true);
        Gate("owner/different world", f => f.Owner.world = new World());
        Gate("owner/null world", f => f.Owner.world = null);
        Gate("owner/despawned", f => f.Owner.IsDespawned = true);
        Gate("owner/marked for unload", f => f.Owner.markedForUnload = true);
        Gate("owner/unloaded", f => f.Owner.isUnloaded = true);
        Gate("owner/disconnected not in world membership", f => f.World.Entities.Remove(f.Owner.entityId));
        Gate("owner/reused entity id not owner instance", f => f.World.Entities[f.Owner.entityId] = new EntityPlayer { entityId = f.Owner.entityId, world = f.World });
        Gate("drone/null", f => f.Drone = null);
        Gate("drone/null world", f => f.Drone.world = null);
        Gate("drone/dead", f => f.Drone.Dead = true);
        Gate("drone/despawned", f => f.Drone.IsDespawned = true);
        Gate("drone/marked for unload", f => f.Drone.markedForUnload = true);
        Gate("drone/unloaded", f => f.Drone.isUnloaded = true);
        Gate("drone/shutdown flag", f => f.Drone.isShutdown = true);
        Gate("drone/shutdown pending", f => f.Drone.isShutdownPending = true);
        Gate("drone/pickup in progress", f => f.Drone.isBeingPickedUp = true);
        Gate("drone/shutdown state", f => f.Drone.CurrentState = EntityDrone.State.Shutdown);
        Gate("drone/NoClip transition state", f => f.Drone.CurrentState = EntityDrone.State.NoClip);
        Gate("drone/Teleport transition state", f => f.Drone.CurrentState = EntityDrone.State.Teleport);
        Gate("drone/None invalid state", f => f.Drone.CurrentState = EntityDrone.State.None);
        Gate("drone/unknown state", f => f.Drone.CurrentState = (EntityDrone.State)999);
        foreach (EntityDrone.State activeStateValue in new[] { EntityDrone.State.Idle, EntityDrone.State.Sentry, EntityDrone.State.Follow, EntityDrone.State.Heal, EntityDrone.State.Attack })
        {
            EntityDrone.State activeState = activeStateValue;
            Test("drone/active state " + activeState, delegate { var f = new Fixture(); f.Drone.CurrentState = activeState; f.Tick(0); Equal(1, f.Zombie.Buffs.Calls.Count, "active state sprays"); });
        }
        Gate("drone/Health 0", f => f.Drone.Health = 0);
        Gate("drone/Health 1", f => f.Drone.Health = 1);
        Gate("drone/Health 2 conservative F10 guard", f => f.Drone.Health = 2);
        Test("drone/Health 3 resumes", delegate { var f = new Fixture(); f.Drone.Health = 3; f.Tick(0); Equal(1, f.Zombie.Buffs.Calls.Count, "Health 3 accepted"); });
        Gate("drone/not deployed in world membership", f => f.World.Entities.Remove(f.Drone.entityId));
        Gate("drone/reused entity id not deployed instance", f => f.World.Entities[f.Drone.entityId] = NewDrone(f.World, f.Owner, f.Drone.entityId));
        Test("drone/deployed membership restored resumes", delegate { var f = new Fixture(); f.World.Entities.Remove(2); f.Tick(0); f.World.Entities[2] = f.Drone; f.Tick(0); Equal(1, f.Zombie.Buffs.Calls.Count, "deployment accepted immediately"); });

        TargetGate("target/dead zombie", f => f.Zombie.Dead = true);
        TargetGate("target/despawned zombie", f => f.Zombie.IsDespawned = true);
        TargetGate("target/marked for unload zombie", f => f.Zombie.markedForUnload = true);
        TargetGate("target/unloaded zombie", f => f.Zombie.isUnloaded = true);
        TargetGate("target/different world zombie", f => f.Zombie.world = new World());
        TargetGate("target/null buffs", f => f.Zombie.Buffs = null);
        TargetGate("target/ordinary zombie lacks eligibility tags", f => f.Zombie.Tags.Clear());
        TargetGate("target/feral alone not eligible", f => f.Zombie.Tags = new HashSet<string> { "feral" });
        foreach (string tagValue in new[] { "radiated", "charged", "infernal" })
        {
            string tag = tagValue;
            Test("target/eligible " + tag, delegate { var f = new Fixture(); f.Zombie.Tags = new HashSet<string> { tag }; f.Tick(0); Equal(1, f.Zombie.Buffs.Calls.Count, "tag accepted"); NativeArgs(f.Zombie); });
        }
        Test("target/combined eligible tags produce one call", delegate { var f = new Fixture(); f.Zombie.Tags = new HashSet<string> { "radiated", "charged", "infernal" }; f.Tick(0); Equal(1, f.Zombie.Buffs.Calls.Count, "single buff"); });
        Test("target/non-zombie player animal vulture null excluded", delegate { var f = new Fixture(); f.World.QueryResults.Clear(); var player = new EntityPlayer { world = f.World }; var animal = new EntityAnimal { world = f.World }; var radiatedVulture = new EntityVulture { world = f.World }; f.World.QueryResults.AddRange(new Entity[] { null, player, animal, radiatedVulture, new EntityAlive { world = f.World } }); f.Tick(0); Equal(0, f.Zombie.Buffs.Calls.Count, "no zombie buff"); Equal(0, player.DamageCalls + animal.DamageCalls + radiatedVulture.DamageCalls, "no damage"); AssertCandidatesCleared(f.Drone); });
        foreach (Type nonZombieType in new[] { typeof(EntityPlayer), typeof(EntityAnimal), typeof(EntityVulture), typeof(EntityAlive) })
        {
            Type targetType = nonZombieType;
            Test("target/radiated non-zombie excluded: " + targetType.Name, delegate
            {
                var f = new Fixture();
                EntityAlive target = (EntityAlive)Activator.CreateInstance(targetType);
                target.world = f.World; target.position = new Vector3(1, 0, 0); target.Tags.Add("radiated");
                f.World.QueryResults.Clear(); f.World.QueryResults.Add(target);
                f.Tick(0);
                Equal(0, target.Buffs.Calls.Count, "non-zombie never buffed even with radiated tag");
                Equal(0, target.DamageCalls + target.AiCalls + target.WakeCalls, "non-zombie no side effects");
                AssertCandidatesCleared(f.Drone);
            });
        }
        Test("target/sleeping zombie stays asleep without damage or AI calls", delegate { var f = new Fixture(); f.Tick(0); f.Tick(1); Assert(f.Zombie.IsSleeping, "sleep state unchanged"); Equal(0, f.Zombie.DamageCalls, "damage calls"); Equal(0, f.Zombie.AiCalls, "AI calls"); Equal(0, f.Zombie.WakeCalls, "wake calls"); NativeArgs(f.Zombie); });
        Test("query/explicit zombie flags and 25m center", delegate { var f = new Fixture(); f.Drone.position = new Vector3(-10, 8, 4); f.Tick(0); Equal(EntityFlags.Zombie, f.World.LastFlags, "query flags"); Equal(25f, f.World.LastRadius, "query radius"); Equal(f.Drone.position.x, f.World.LastCenter.x, "center x"); Equal(f.Drone.position.y, f.World.LastCenter.y, "center y"); Equal(f.Drone.position.z, f.World.LastCenter.z, "center z"); });

        Test("cadence/query limit once per second including boundary", delegate { var f = new Fixture(); foreach (float t in new[] { 0f, 0f, .1f, .5f, .999f }) f.Tick(t); Equal(1, f.World.QueryCount, "one query before 1s"); f.Tick(1); Equal(2, f.World.QueryCount, "query exactly 1s"); f.Tick(1.999f); Equal(2, f.World.QueryCount, "still two queries"); f.Tick(2); Equal(3, f.World.QueryCount, "query exactly 2s"); Equal(3, f.Zombie.Buffs.Calls.Count, "three target refreshes"); });
        Test("cadence/two drones deduplicate once per target per second", delegate { var f = new Fixture(); EntityDrone other = NewDrone(f.World, f.Owner, 20); f.World.Entities[20] = other; f.Tick(0); SprayerController.Tick(other); Equal(2, f.World.QueryCount, "each drone one query"); Equal(1, f.Zombie.Buffs.Calls.Count, "single target refresh"); f.Tick(.5f); SprayerController.Tick(other); Equal(2, f.World.QueryCount, "no midsecond queries"); f.Tick(1); SprayerController.Tick(other); Equal(4, f.World.QueryCount, "two next-second queries"); Equal(2, f.Zombie.Buffs.Calls.Count, "single refresh each second"); NativeArgs(f.Zombie); });
        Test("cadence/duplicate broad-phase entries deduplicate", delegate { var f = new Fixture(); f.World.QueryResults.Add(f.Zombie); f.World.QueryResults.Add(f.Zombie); f.Tick(0); Equal(1, f.Zombie.Buffs.Calls.Count, "duplicate target once"); });
        Test("cadence/100 pulses do not grow shared duration or stack", delegate { var f = new Fixture(); for (int i = 0; i < 100; i++) { f.Tick(i); Equal(15f, f.Zombie.Buffs.ExpiresAt - i, "controlled refresh remains 15s"); Equal(1, f.Zombie.Buffs.ActiveBuffCount, "controlled buff remains single"); Equal(15f, BuffClass.Duration, "native duration untouched"); } Equal(100, f.Zombie.Buffs.Calls.Count, "one call per second"); NativeArgs(f.Zombie); });
        Test("cadence/removal does not refresh existing timed inhibitor", delegate { var f = new Fixture(); f.Tick(0); Equal(15f, f.Zombie.Buffs.ExpiresAt, "controlled initial expiry"); f.Drone.OriginalItemValue.Mods = new ItemValue[0]; for (int t = 1; t <= 20; t++) f.Tick(t); Equal(15f, f.Zombie.Buffs.ExpiresAt, "no expiry extension after removal"); Equal(1, f.Zombie.Buffs.Calls.Count, "no additional calls"); });
        Test("cadence/backward clock resets drone and target cooldown", delegate { var f = new Fixture(); f.Tick(100); f.Tick(90); Equal(2, f.World.QueryCount, "backward clock query"); Equal(2, f.Zombie.Buffs.Calls.Count, "backward clock target refresh"); f.Tick(90.5f); Equal(2, f.World.QueryCount, "new cooldown applies"); f.Tick(91); Equal(3, f.Zombie.Buffs.Calls.Count, "new timeline advances"); });
        Test("cadence/non-finite times ignored", delegate { var f = new Fixture(); f.Tick(float.NaN); f.Tick(float.PositiveInfinity); f.Tick(float.NegativeInfinity); Equal(0, f.World.QueryCount, "non-finite no query"); f.Tick(0); Equal(1, f.Zombie.Buffs.Calls.Count, "valid time still works"); });
        Test("state/reused target id does not inherit cooldown", delegate { var f = new Fixture(); f.Tick(0); EntityZombie replacement = new EntityZombie { entityId = f.Zombie.entityId, world = f.World }; f.World.QueryResults.Clear(); f.World.QueryResults.Add(replacement); EntityDrone other = NewDrone(f.World, f.Owner, 20); f.World.Entities[20] = other; Time.time = .5f; SprayerController.Tick(other); Equal(1, replacement.Buffs.Calls.Count, "replacement refreshed immediately"); });
        Test("state/reused drone id does not inherit cooldown", delegate { var f = new Fixture(); f.Tick(0); f.Drone = NewDrone(f.World, f.Owner, f.Drone.entityId); f.World.Entities[2] = f.Drone; f.Zombie = new EntityZombie { world = f.World }; f.World.QueryResults.Clear(); f.World.QueryResults.Add(f.Zombie); f.Tick(.5f); Equal(2, f.World.QueryCount, "replacement drone queries immediately"); Equal(1, f.Zombie.Buffs.Calls.Count, "replacement target refreshed"); });
        Test("state/Reset clears cooldowns", delegate { var f = new Fixture(); f.Tick(0); SprayerController.Reset(); f.Tick(.5f); Equal(2, f.Zombie.Buffs.Calls.Count, "reset removes old target cooldown"); });

        Test("cleanup/candidate list cleared after successful pulse", delegate { var f = new Fixture(); f.Tick(0); AssertCandidatesCleared(f.Drone); });
        Test("cleanup/query throws after candidate append", delegate { var f = new Fixture(); f.World.ThrowOnQuery = true; f.Tick(0); AssertCandidatesCleared(f.Drone); Equal(1, Debug.Warnings.Count, "exception logged"); f.World.ThrowOnQuery = false; f.Tick(1); Equal(1, f.Zombie.Buffs.Calls.Count, "recovers next pulse"); AssertCandidatesCleared(f.Drone); });
        Test("cleanup/AddBuff throws", delegate { var f = new Fixture(); f.Zombie.Buffs.ThrowOnAdd = true; f.Tick(0); AssertCandidatesCleared(f.Drone); Equal(1, Debug.Warnings.Count, "exception logged"); f.Zombie.Buffs.ThrowOnAdd = false; f.Tick(1); Equal(2, f.Zombie.Buffs.Calls.Count, "recovers next pulse"); });
        Test("cleanup/third-party tag predicate throws", delegate { var f = new Fixture(); f.Zombie.ThrowOnTags = true; f.Tick(0); AssertCandidatesCleared(f.Drone); Equal(1, Debug.Warnings.Count, "exception logged"); });
        Test("cleanup/world unload nulls world during query", delegate { var f = new Fixture(); f.World.OnQuery = delegate { f.Drone.world = null; }; f.Tick(0); AssertCandidatesCleared(f.Drone); Equal(0, f.Zombie.Buffs.Calls.Count, "unloaded world skipped"); });
        Test("cleanup/world lookup throws without breaking caller", delegate { var f = new Fixture(); f.World.ThrowOnGetEntity = true; f.Tick(0); Equal(0, f.World.QueryCount, "no query after lookup failure"); Equal(1, Debug.Warnings.Count, "exception logged"); });
        Test("cleanup/unload after pulse leaves no strong candidate refs", delegate { var f = new Fixture(); f.Tick(0); AssertCandidatesCleared(f.Drone); f.Drone.isUnloaded = true; f.Tick(1); AssertCandidatesCleared(f.Drone); Equal(1, f.World.QueryCount, "no unloaded pulse"); });
        Test("diagnostics/repeated failures rate limited to 30 seconds", delegate { var f = new Fixture(); f.World.ThrowOnQuery = true; f.Tick(0); f.Tick(1); f.Tick(29); Equal(1, Debug.Warnings.Count, "one warning before 30s"); f.Tick(30); Equal(2, Debug.Warnings.Count, "warning at 30s"); });

        Test("harmony/real PatchAll registers production postfix and preserves original", delegate
        {
            var f = new Fixture();
            new DroneRadiationSprayerMod().InitMod(new Mod());
            MethodInfo original = typeof(EntityDrone).GetMethod("OnUpdateEntity");
            Patches info = Harmony.GetPatchInfo(original);
            Assert(info != null, "real Harmony patch metadata exists");
            Assert(info.Owners.Contains("itachi.droneradiationsprayer"), "production Harmony owner registered");
            Assert(info.Postfixes.Count > 0, "postfix installed");
            Equal(0, info.Prefixes.Count, "no original-skipping prefix");
            f.Drone.OnUpdateEntity();
            Equal(1, f.Drone.OriginalUpdates, "original update executed");
            Equal(1, f.Zombie.Buffs.Calls.Count, "actual postfix executed");
            NativeArgs(f.Zombie);
            Time.time = 1;
            f.Drone.OnOriginalUpdate = delegate { f.Drone.isShutdown = true; };
            f.Drone.OnUpdateEntity();
            Equal(2, f.Drone.OriginalUpdates, "original update executed again");
            Equal(1, f.Zombie.Buffs.Calls.Count, "postfix sees original's final shutdown state");
            new Harmony("itachi.droneradiationsprayer").UnpatchSelf();
            f.Drone.OnOriginalUpdate = null; f.Drone.isShutdown = false;
            Time.time = 2; f.Drone.OnUpdateEntity();
            Equal(3, f.Drone.OriginalUpdates, "unpatched original executes");
            Equal(1, f.Zombie.Buffs.Calls.Count, "unpatch removes real postfix");
        });
        Console.WriteLine("\nRESULT: " + passed + " passed; " + failed + " failed; " + (passed + failed) + " total controlled cases.");
        Console.WriteLine("Global BuffClass duration stayed 15f in every passing case.");
        Console.WriteLine("Native Unity timing, multiplayer replication, actual buff XML execution, F10 third-party integration, and in-game behavior remain UNTESTED.");
        return failed == 0 ? 0 : 1;
    }
}
