using Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Core.Persistence
{
    /// <summary>
    /// Generic save container: header + keyed, length-prefixed section blobs.
    /// Forward compatible: unknown keys are skipped safely.
    /// </summary>
    public static class PersistenceIO
    {
        // -----------------------------------------------------------------------------
        // File Signature ("Magic")
        // -----------------------------------------------------------------------------
        // The "Magic" string is a fixed 4-byte identifier written at the very
        // beginning of every save file.
        //
        // Purpose:
        // - Confirms the file is of the expected type before attempting to read it.
        // - Prevents interpreting unrelated or corrupted files as valid saves.
        // - Allows quick rejection of incompatible formats.
        //
        // If the loaded file's first 4 bytes do not match this value,
        // loading is aborted immediately.
        //
        // Note:
        // - Keep this short (4–8 ASCII bytes).
        // - Change it only if you intentionally create a completely new
        //   and incompatible file format.
        // - This is separate from the container version number.
        // -----------------------------------------------------------------------------
        private const string Magic = "SAV1";
        // -----------------------------------------------------------------------------
        // Container Version
        // -----------------------------------------------------------------------------
        // The container version defines the binary layout of the save file *itself*.
        //
        // This version applies to the outer structure:
        //   - Header layout
        //   - How sections are stored (key + version + length + blob)
        //   - Ordering rules or container-wide metadata
        //
        // This is NOT the same as section versions.
        //
        // Section Version:
        //   Each IDataSection has its own Version property.
        //   That version applies only to that section's internal data format.
        //
        // In short:
        //   - Magic       -> Identifies the file type
        //   - ContainerVersion -> Defines the overall save container structure
        //   - Section.Version  -> Defines each subsystem's data layout
        //
        // Only increment ContainerVersion if you change the top-level file structure.
        // Increment a section's Version when that specific subsystem changes.
        // -----------------------------------------------------------------------------
        private const int ContainerVersion = 1;

        public static void SaveToFile(
            string filePath,
            SaveContext ctx,
            IReadOnlyList<IDataSection> sections)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Invalid file path.", nameof(filePath));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (sections == null) throw new ArgumentNullException(nameof(sections));

            Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? ".");

            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            SaveToStream(fs, ctx, sections);
        }

        public static void SaveToStream(
            Stream stream,
            SaveContext ctx,
            IReadOnlyList<IDataSection> sections)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanWrite) throw new ArgumentException("Stream must be writable.", nameof(stream));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (sections == null) throw new ArgumentNullException(nameof(sections));

            using var w = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

            // Header
            w.Write(Encoding.ASCII.GetBytes(Magic)); // 4 bytes
            w.Write(ContainerVersion);

            // Record count
            w.Write(sections.Count);

            // Records
            foreach (var s in sections)
            {
                if (s == null) continue;

                w.Write(s.Key);
                w.Write(s.Version);

                // Length-prefixed blob allows skipping unknown keys later.
                using var ms = new MemoryStream();
                using (var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
                {
                    s.Write(ctx, bw);
                }

                var bytes = ms.ToArray();
                w.Write(bytes.Length);
                w.Write(bytes);
            }
        }

        public static void LoadFromFile(
            string filePath,
            SaveContext ctx,
            IReadOnlyDictionary<string, IDataSection> sectionsByKey)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Invalid file path.", nameof(filePath));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (sectionsByKey == null) throw new ArgumentNullException(nameof(sectionsByKey));

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            LoadFromStream(fs, ctx, sectionsByKey);
        }

        public static void LoadFromStream(
            Stream stream,
            SaveContext ctx,
            IReadOnlyDictionary<string, IDataSection> sectionsByKey)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new ArgumentException("Stream must be readable.", nameof(stream));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (sectionsByKey == null) throw new ArgumentNullException(nameof(sectionsByKey));

            using var r = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            // Header validation
            var magic = Encoding.ASCII.GetString(r.ReadBytes(4));
            if (magic != Magic)
                throw new InvalidDataException($"Not a supported save file. Magic={magic}");

            int containerVersion = r.ReadInt32();
            if (containerVersion != ContainerVersion)
                throw new InvalidDataException($"Unsupported container version {containerVersion}");

            int recordCount = r.ReadInt32();
            if (recordCount < 0)
                throw new InvalidDataException("Corrupt save: negative recordCount.");

            // Records
            for (int i = 0; i < recordCount; i++)
            {
                string key = r.ReadString();
                int version = r.ReadInt32();
                int len = r.ReadInt32();

                if (len < 0)
                    throw new InvalidDataException($"Corrupt save: negative record length for key '{key}'.");

                var bytes = r.ReadBytes(len);
                if (bytes.Length != len)
                    throw new EndOfStreamException($"Unexpected EOF reading section '{key}'.");

                if (!sectionsByKey.TryGetValue(key, out var section) || section == null)
                {
                    // Unknown section => ignore for forward compatibility.
                    continue;
                }

                using var ms = new MemoryStream(bytes);
                using var br = new BinaryReader(ms, Encoding.UTF8, leaveOpen: false);
                section.Read(ctx, br, version);
            }
        }
    }
}
