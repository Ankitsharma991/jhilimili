using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UtsavNights.Core.Home;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Loads and saves house.json (PRD 13.5) and keeps the edit journal beside it (house.journal). A missing file gives the
    /// default house; a corrupt one is replaced by the default, never a crash. <see cref="Open"/> returns an editor that
    /// has already replayed any session killed before its save.
    /// </summary>
    public sealed class HouseStore
    {
        public const string FileName = "house.json";
        public const string JournalName = "house.journal";

        public HouseStore(string path = null)
        {
            Path = path ?? System.IO.Path.Combine(Application.persistentDataPath, FileName);
            Journal = new FileJournalStore(System.IO.Path.ChangeExtension(Path, ".journal"));
        }

        public string Path { get; }

        public FileJournalStore Journal { get; }

        /// <summary>Lines replayed by the last <see cref="Open"/>; above zero means an interrupted edit was restored.</summary>
        public int Recovered { get; private set; }

        public HouseState Load()
        {
            if (!File.Exists(Path))
            {
                return HouseState.Fresh();
            }

            try
            {
                HouseState loaded = JsonUtility.FromJson<HouseState>(File.ReadAllText(Path));
                if (loaded == null)
                {
                    return HouseState.Fresh();
                }

                if (loaded.Migrate() | loaded.Sanitize())
                {
                    Write(loaded);
                }

                return loaded;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[House] {Path} unreadable, using the default house: {e.Message}");
                return HouseState.Fresh();
            }
        }

        public HouseEditor Open()
        {
            var editor = new HouseEditor(Load(), Journal);
            Recovered = editor.Recover();
            if (Recovered > 0)
            {
                Debug.Log($"[House] restored {Recovered} journalled edits from an interrupted session");
            }

            return editor;
        }

        /// <summary>Saves the editor's state and starts a fresh journal. Also call after saving or deleting a layout.</summary>
        public bool Save(HouseEditor editor)
        {
            HouseState state = editor.BeginSave();
            if (!Write(state))
            {
                editor.SaveFailed();
                return false;
            }

            editor.SaveWritten();
            return true;
        }

        public void Delete()
        {
            foreach (string p in new[] { Path, Journal.Path })
            {
                if (File.Exists(p))
                {
                    File.Delete(p);
                }
            }
        }

        private bool Write(HouseState state)
        {
            try
            {
                AtomicFile.WriteAllText(Path, JsonUtility.ToJson(state, true));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[House] could not save {Path}: {e.Message}");
                return false;
            }
        }
    }

    /// <summary>The journal as a text file, one line per entry. Each append is flushed before it returns.</summary>
    public sealed class FileJournalStore : IJournalStore
    {
        public FileJournalStore(string path) => Path = path;

        public string Path { get; }

        public IReadOnlyList<string> ReadLines()
        {
            try
            {
                return File.Exists(Path) ? File.ReadAllLines(Path) : Array.Empty<string>();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[House] journal unreadable: {e.Message}");
                return Array.Empty<string>();
            }
        }

        public void Append(string line)
        {
            try
            {
                using var stream = new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(stream);
                writer.Write(line);
                writer.Write('\n');
                writer.Flush();
                stream.Flush(true);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[House] journal append failed: {e.Message}");
            }
        }

        public void Reset(string firstLine)
        {
            try
            {
                AtomicFile.WriteAllText(Path, firstLine + "\n");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[House] journal reset failed: {e.Message}");
            }
        }
    }
}
