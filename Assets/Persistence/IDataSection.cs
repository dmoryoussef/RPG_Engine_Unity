using System.IO;

namespace Persistence
{
    /// <summary>
    /// A pluggable save/load section stored inside the save container as a keyed record.
    /// </summary>
    public interface IDataSection
    {
        /// <summary>Stable key used to identify this record in the save file.</summary>
        string Key { get; }

        /// <summary>Per-section version. Increment when the section's binary layout changes.</summary>
        int Version { get; }

        /// <summary>Write this section into the provided writer.</summary>
        void Write(SaveContext ctx, BinaryWriter w);

        /// <summary>Read this section from the provided reader.</summary>
        void Read(SaveContext ctx, BinaryReader r, int version);
    }
}
