// Stand-ins for the handful of Unity types the simulation and its edit-mode
// tests touch, so both can be compiled and run outside the Unity editor.
//
// The simulation itself uses exactly one Unity type ([SerializeField], purely
// as a serialization marker), and the edit-mode tests use two more
// (Object.DestroyImmediate and AssetDatabase.LoadAssetAtPath). Nothing here
// influences a simulation result: these are attributes and no-ops. Keeping the
// list this short is deliberate -- if it ever grows, the simulation has started
// depending on the engine and that is worth noticing.

namespace UnityEngine
{
    /// <summary>Marks a private field for Unity's serializer. No runtime behaviour.</summary>
    [System.AttributeUsage(System.AttributeTargets.Field, AllowMultiple = false)]
    public sealed class SerializeField : System.Attribute
    {
    }

    /// <summary>Adds an entry to Unity's asset-creation menu. No runtime behaviour.</summary>
    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false)]
    public sealed class CreateAssetMenu : System.Attribute
    {
        public string fileName;
        public string menuName;
        public int order;
    }

    /// <summary>The root of Unity's object model, reduced to what the tests call.</summary>
    public class Object
    {
        /// <summary>
        /// A no-op here. In the editor this frees an in-memory asset; outside
        /// it, the garbage collector does that job, and the tests only ever
        /// call it on a scenario they are finished with.
        /// </summary>
        public static void DestroyImmediate(Object target)
        {
        }
    }

    /// <summary>
    /// An asset that lives as data rather than in a scene. The real class does
    /// a great deal more; the tests only ever ask for a fresh in-memory
    /// instance holding the code defaults, which is what CreateInstance gives.
    /// </summary>
    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new()
        {
            return new T();
        }
    }
}

namespace UnityEditor
{
    /// <summary>
    /// Unity's asset database. Outside the editor there is none, so loading
    /// always fails; the one test that needs it is excluded by the runner and
    /// reported as skipped rather than passed.
    /// </summary>
    public static class AssetDatabase
    {
        public static T LoadAssetAtPath<T>(string assetPath) where T : class
        {
            return null;
        }
    }
}
