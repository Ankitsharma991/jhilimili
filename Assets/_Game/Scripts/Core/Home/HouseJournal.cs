using System;
using System.Collections.Generic;
using System.Globalization;

namespace UtsavNights.Core.Home
{
    /// <summary>Where journal lines live: a file on the phone, memory in tests. Appends must reach storage before returning.</summary>
    public interface IJournalStore
    {
        IReadOnlyList<string> ReadLines();

        void Append(string line);

        /// <summary>Replaces the whole journal with one line.</summary>
        void Reset(string firstLine);
    }

    public sealed class MemoryJournalStore : IJournalStore
    {
        public List<string> Lines { get; } = new List<string>();

        public IReadOnlyList<string> ReadLines() => Lines.ToArray();

        public void Append(string line) => Lines.Add(line);

        public void Reset(string firstLine)
        {
            Lines.Clear();
            Lines.Add(firstLine);
        }
    }

    /// <summary>
    /// The append-only edit journal (PRD 9.1). Line one is "B|serial", naming the house.json save the session started
    /// from. Then one line per action: "D|command" for an edit, "U" for undo and "R" for redo. Every line ends with
    /// "#" and an FNV-1a checksum, so a line torn by a kill mid-write is recognised and replay stops before it.
    /// </summary>
    public static class HouseJournal
    {
        public static string Base(int serial) => Seal("B|" + serial.ToString(CultureInfo.InvariantCulture));

        public static string Did(IHouseCommand command) => Seal("D|" + HouseCommandCodec.Encode(command));

        public static string Undid() => Seal("U");

        public static string Redid() => Seal("R");

        /// <summary>The body of a sealed line, or null when the checksum is missing or wrong.</summary>
        public static string Open(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return null;
            }

            int hash = line.LastIndexOf('#');
            if (hash < 0)
            {
                return null;
            }

            string body = line.Substring(0, hash);
            return line.Substring(hash + 1) == Checksum(body) ? body : null;
        }

        public static string Seal(string body) => body + "#" + Checksum(body);

        public static string Checksum(string body)
        {
            uint h = 2166136261;
            foreach (char c in body)
            {
                h = (h ^ c) * 16777619;
            }

            return h.ToString("x8", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// The decoration-mode editing session: the house state, its undo history and the journal (PRD 9.1). Every edit,
    /// undo and redo is journalled before the call returns; <see cref="Recover"/> replays a session that was killed
    /// before its save. Saving (<see cref="BeginSave"/>, write house.json, <see cref="SaveWritten"/>) bumps the serial
    /// and starts a fresh journal. Undo does not reach past a save, so a replayed journal always starts from the save
    /// on disk.
    /// </summary>
    public sealed class HouseEditor
    {
        private readonly IJournalStore _journal;
        private readonly EditHistory _history;
        private string _savedFingerprint;

        public HouseEditor(HouseState saved, IJournalStore journal, int undoSteps = EditHistory.DefaultCapacity)
        {
            State = saved ?? HouseState.Fresh();
            _journal = journal;
            _history = new EditHistory(undoSteps);
            _savedFingerprint = State.design.Fingerprint();
        }

        public event Action Changed;

        public HouseState State { get; }

        public HouseDesign Design => State.design;

        public bool CanUndo => _history.UndoCount > 0;

        public bool CanRedo => _history.RedoCount > 0;

        public int UndoCount => _history.UndoCount;

        public int RedoCount => _history.RedoCount;

        /// <summary>True when the design differs from the last save (or from what was loaded).</summary>
        public bool Unsaved => Design.Fingerprint() != _savedFingerprint;

        /// <summary>
        /// Replays the journal left by a session that ended without saving. Returns how many lines were replayed. A
        /// journal from another save (an older serial, or none) is discarded. The journal is rewritten as replayed, so a
        /// second crash right after recovery loses nothing either.
        /// </summary>
        public int Recover()
        {
            IReadOnlyList<string> lines = _journal.ReadLines();
            var actions = new List<string>();
            if (lines.Count > 0 && HouseJournal.Open(lines[0]) == "B|" + State.saveSerial.ToString(CultureInfo.InvariantCulture))
            {
                for (int i = 1; i < lines.Count; i++)
                {
                    string body = HouseJournal.Open(lines[i]);
                    if (body == null)
                    {
                        break;
                    }

                    actions.Add(body);
                }
            }

            _journal.Reset(HouseJournal.Base(State.saveSerial));
            int replayed = 0;
            foreach (string body in actions)
            {
                bool ok = body == "U" ? Undo() : body == "R" ? Redo() : body.StartsWith("D|", StringComparison.Ordinal) && Do(HouseCommandCodec.Decode(body.Substring(2)));
                if (!ok)
                {
                    break;
                }

                replayed++;
            }

            return replayed;
        }

        public bool Do(IHouseCommand command)
        {
            if (command == null || !_history.Do(command, Design))
            {
                return false;
            }

            _journal.Append(HouseJournal.Did(command));
            Changed?.Invoke();
            return true;
        }

        public bool Undo()
        {
            if (!_history.Undo(Design))
            {
                return false;
            }

            _journal.Append(HouseJournal.Undid());
            Changed?.Invoke();
            return true;
        }

        public bool Redo()
        {
            if (!_history.Redo(Design))
            {
                return false;
            }

            _journal.Append(HouseJournal.Redid());
            Changed?.Invoke();
            return true;
        }

        /// <summary>Bumps the save serial and returns the state to write to house.json.</summary>
        public HouseState BeginSave()
        {
            State.saveSerial++;
            return State;
        }

        /// <summary>
        /// Call once house.json is safely written. If the app dies between the write and this call, the journal still names
        /// the old serial and is discarded on the next start, so no edit is applied twice.
        /// </summary>
        public void SaveWritten()
        {
            _journal.Reset(HouseJournal.Base(State.saveSerial));
            _history.Clear();
            _savedFingerprint = Design.Fingerprint();
            Changed?.Invoke();
        }

        /// <summary>The write failed: the serial goes back so the journal still matches the save on disk.</summary>
        public void SaveFailed() => State.saveSerial--;

        public bool SaveLayout(int slot, string name, int layoutSlots = HouseState.FreeLayouts) => State.SaveLayout(slot, name, layoutSlots);

        public bool LoadLayout(int slot) => slot >= 0 && slot < State.layouts.Count && Do(new LoadDesign(State.layouts[slot].design));

        /// <summary>Places a copy of an item next to the original (PRD 9.1: duplicate), offset by <paramref name="dx"/>, <paramref name="dz"/>.</summary>
        public bool Duplicate(int uid, float dx, float dz)
        {
            PlacedDecor original = Design.Find(uid);
            if (original == null)
            {
                return false;
            }

            PlacedDecor copy = original.Clone();
            copy.x += dx;
            copy.z += dz;
            copy.ex += dx;
            copy.ez += dz;
            return Do(PlaceDecor.New(Design, copy));
        }
    }
}
