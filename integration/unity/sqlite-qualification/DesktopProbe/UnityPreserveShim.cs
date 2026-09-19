// The candidate makes sqlite-net's mapping attributes derive from Unity's stripping marker.
// This probe-only shim supplies that marker to a plain .NET host; it has no behavior.
namespace UnityEngine.Scripting
{
    public class PreserveAttribute : System.Attribute
    {
    }
}
