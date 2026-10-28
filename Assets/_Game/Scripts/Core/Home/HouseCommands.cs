using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace UtsavNights.Core.Home
{
    /// <summary>
    /// One undoable edit (PRD 9.1). <see cref="Apply"/> records whatever it needs to put the design back exactly, and
    /// returns false (changing nothing) when the edit no longer makes sense, such as moving a deleted item.
    /// </summary>
    public interface IHouseCommand
    {
        /// <summary>Short name for analytics and logs: place, remove, pose, module, paint, layout.</summary>
        string Kind { get; }

        bool Apply(HouseDesign design);

        void Revert(HouseDesign design);

        void Encode(CommandWriter w);
    }

    public sealed class PlaceDecor : IHouseCommand
    {
        private readonly PlacedDecor _item;
        private int _previousNextUid;

        /// <summary>The caller takes the uid from <see cref="HouseDesign.nextUid"/>, so replaying gives the same uid.</summary>
        public PlaceDecor(PlacedDecor item) => _item = item.Clone();

        public string Kind => "place";

        public int Uid => _item.uid;

        public static PlaceDecor New(HouseDesign design, PlacedDecor pose)
        {
            PlacedDecor item = pose.Clone();
            item.uid = design.nextUid;
            return new PlaceDecor(item);
        }

        public bool Apply(HouseDesign design)
        {
            if (_item.uid <= 0 || design.Find(_item.uid) != null || HomeCatalog.Decoration(_item.item) == null)
            {
                return false;
            }

            _previousNextUid = design.nextUid;
            design.decorations.Add(_item.Clone());
            design.nextUid = Math.Max(design.nextUid, _item.uid + 1);
            return true;
        }

        public void Revert(HouseDesign design)
        {
            int i = design.IndexOf(_item.uid);
            if (i >= 0)
            {
                design.decorations.RemoveAt(i);
            }

            design.nextUid = _previousNextUid;
        }

        public void Encode(CommandWriter w) => w.Decor(_item);
    }

    public sealed class RemoveDecor : IHouseCommand
    {
        private readonly int _uid;
        private PlacedDecor _removed;
        private int _index;

        public RemoveDecor(int uid) => _uid = uid;

        public string Kind => "remove";

        public bool Apply(HouseDesign design)
        {
            _index = design.IndexOf(_uid);
            if (_index < 0)
            {
                return false;
            }

            _removed = design.decorations[_index];
            design.decorations.RemoveAt(_index);
            return true;
        }

        public void Revert(HouseDesign design) => design.decorations.Insert(Math.Min(_index, design.decorations.Count), _removed.Clone());

        public void Encode(CommandWriter w) => w.Int(_uid);
    }

    /// <summary>Move, rotate, scale, re-hang (path ends and sag) or recolour one placed item.</summary>
    public sealed class PoseDecor : IHouseCommand
    {
        private readonly int _uid;
        private readonly PlacedDecor _pose;
        private PlacedDecor _before;

        public PoseDecor(int uid, PlacedDecor pose)
        {
            _uid = uid;
            _pose = pose.Clone();
        }

        public string Kind => "pose";

        public bool Apply(HouseDesign design)
        {
            PlacedDecor target = design.Find(_uid);
            if (target == null)
            {
                return false;
            }

            _before = target.Clone();
            target.CopyPoseFrom(_pose);
            return true;
        }

        public void Revert(HouseDesign design) => design.Find(_uid)?.CopyPoseFrom(_before);

        public void Encode(CommandWriter w)
        {
            w.Int(_uid);
            w.Decor(_pose);
        }
    }

    public sealed class SetModule : IHouseCommand
    {
        private readonly string _point;
        private readonly string _module;
        private readonly int _variant;
        private ModuleChoice _before;

        public SetModule(string point, string module, int variant = 0)
        {
            _point = point;
            _module = module ?? string.Empty;
            _variant = variant;
        }

        public string Kind => "module";

        public bool Apply(HouseDesign design)
        {
            AttachmentPoint point = HomeCatalog.Point(_point);
            ModuleChoice slot = design.ModuleAt(_point);
            if (point == null || slot == null)
            {
                return false;
            }

            if (_module.Length == 0 ? !point.Optional : !Fits(point, HomeCatalog.Module(_module), _variant))
            {
                return false;
            }

            _before = slot.Clone();
            slot.module = _module;
            slot.variant = _module.Length == 0 ? 0 : _variant;
            return true;
        }

        public void Revert(HouseDesign design)
        {
            ModuleChoice slot = design.ModuleAt(_point);
            if (slot != null)
            {
                slot.module = _before.module;
                slot.variant = _before.variant;
            }
        }

        public void Encode(CommandWriter w)
        {
            w.Str(_point);
            w.Str(_module);
            w.Int(_variant);
        }

        private static bool Fits(AttachmentPoint point, ModuleInfo module, int variant) =>
            module != null && module.Type == point.Type && variant >= 0 && variant < module.Variants;
    }

    /// <summary>Wall colour, trim colour and wall material, set together so one colour pick is one undo step.</summary>
    public sealed class SetPaint : IHouseCommand
    {
        private readonly int _wall;
        private readonly int _trim;
        private readonly int _material;
        private int _beforeWall;
        private int _beforeTrim;
        private int _beforeMaterial;

        public SetPaint(int wall, int trim, int material)
        {
            _wall = wall;
            _trim = trim;
            _material = material;
        }

        public string Kind => "paint";

        public bool Apply(HouseDesign design)
        {
            if (!In(_wall, HomeCatalog.WallColours.Length) || !In(_trim, HomeCatalog.TrimColours.Length) || !In(_material, HomeCatalog.Materials.Length))
            {
                return false;
            }

            _beforeWall = design.wallColour;
            _beforeTrim = design.trimColour;
            _beforeMaterial = design.material;
            design.wallColour = _wall;
            design.trimColour = _trim;
            design.material = _material;
            return true;
        }

        public void Revert(HouseDesign design)
        {
            design.wallColour = _beforeWall;
            design.trimColour = _beforeTrim;
            design.material = _beforeMaterial;
        }

        public void Encode(CommandWriter w)
        {
            w.Int(_wall);
            w.Int(_trim);
            w.Int(_material);
        }

        private static bool In(int v, int count) => v >= 0 && v < count;
    }

    /// <summary>Replaces the whole design, as when a saved layout is loaded. The design travels with the command, so the
    /// journal replays it even if the layout slot changes later.</summary>
    public sealed class LoadDesign : IHouseCommand
    {
        private readonly HouseDesign _design;
        private HouseDesign _before;

        public LoadDesign(HouseDesign design)
        {
            _design = design.Clone();
            _design.Sanitize();
        }

        public string Kind => "layout";

        public bool Apply(HouseDesign design)
        {
            _before = design.Clone();
            CopyInto(_design, design);
            return true;
        }

        public void Revert(HouseDesign design) => CopyInto(_before, design);

        public void Encode(CommandWriter w)
        {
            w.Str(_design.kitId);
            w.Int(_design.wallColour);
            w.Int(_design.trimColour);
            w.Int(_design.material);
            w.Int(_design.nextUid);
            w.Int(_design.modules.Count);
            foreach (ModuleChoice m in _design.modules)
            {
                w.Str(m.point);
                w.Str(m.module);
                w.Int(m.variant);
            }

            w.Int(_design.decorations.Count);
            foreach (PlacedDecor d in _design.decorations)
            {
                w.Decor(d);
            }
        }

        internal static HouseDesign DecodeDesign(CommandReader r)
        {
            var d = new HouseDesign { kitId = r.Str(), wallColour = r.Int(), trimColour = r.Int(), material = r.Int(), nextUid = r.Int() };
            int modules = r.Count();
            for (int i = 0; i < modules; i++)
            {
                d.modules.Add(new ModuleChoice(r.Str(), r.Str(), r.Int()));
            }

            int decorations = r.Count();
            for (int i = 0; i < decorations; i++)
            {
                d.decorations.Add(r.Decor());
            }

            return d;
        }

        private static void CopyInto(HouseDesign from, HouseDesign to)
        {
            HouseDesign copy = from.Clone();
            to.kitId = copy.kitId;
            to.modules = copy.modules;
            to.wallColour = copy.wallColour;
            to.trimColour = copy.trimColour;
            to.material = copy.material;
            to.decorations = copy.decorations;
            to.nextUid = copy.nextUid;
        }
    }

    /// <summary>Several commands as one undo step (both ground-floor windows at once). All apply, or none.</summary>
    public sealed class CommandBatch : IHouseCommand
    {
        private readonly IHouseCommand[] _commands;

        public CommandBatch(params IHouseCommand[] commands) => _commands = commands;

        public string Kind => "batch";

        public bool Apply(HouseDesign design)
        {
            for (int i = 0; i < _commands.Length; i++)
            {
                if (!_commands[i].Apply(design))
                {
                    for (int j = i - 1; j >= 0; j--)
                    {
                        _commands[j].Revert(design);
                    }

                    return false;
                }
            }

            return _commands.Length > 0;
        }

        public void Revert(HouseDesign design)
        {
            for (int i = _commands.Length - 1; i >= 0; i--)
            {
                _commands[i].Revert(design);
            }
        }

        public void Encode(CommandWriter w)
        {
            w.Int(_commands.Length);
            foreach (IHouseCommand c in _commands)
            {
                w.Str(HouseCommandCodec.Encode(c));
            }
        }

        internal static CommandBatch Decode(CommandReader r)
        {
            int n = r.Count();
            var list = new IHouseCommand[n];
            for (int i = 0; i < n; i++)
            {
                list[i] = HouseCommandCodec.Decode(r.Str()) ?? throw new FormatException("bad batch item");
            }

            return new CommandBatch(list);
        }
    }

    /// <summary>Commands as text for the journal: a tag, then fields separated by '|', invariant culture, ids escaped.</summary>
    public static class HouseCommandCodec
    {
        public static string Encode(IHouseCommand command)
        {
            var w = new CommandWriter();
            w.Str(command.Kind);
            command.Encode(w);
            return w.ToString();
        }

        /// <summary>Null for anything it cannot read: an unknown tag, too few fields or a bad number.</summary>
        public static IHouseCommand Decode(string text)
        {
            try
            {
                var r = new CommandReader(text);
                IHouseCommand command = r.Str() switch
                {
                    "place" => new PlaceDecor(r.Decor()),
                    "remove" => new RemoveDecor(r.Int()),
                    "pose" => new PoseDecor(r.Int(), r.Decor()),
                    "module" => new SetModule(r.Str(), r.Str(), r.Int()),
                    "paint" => new SetPaint(r.Int(), r.Int(), r.Int()),
                    "layout" => new LoadDesign(LoadDesign.DecodeDesign(r)),
                    "batch" => CommandBatch.Decode(r),
                    _ => null,
                };
                return command != null && r.AtEnd ? command : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }

    public sealed class CommandWriter
    {
        private readonly StringBuilder _sb = new StringBuilder();

        public void Str(string s)
        {
            Separator();
            foreach (char c in s ?? string.Empty)
            {
                // '%' and '|' are escaped so ids and names never split a field; line breaks never reach the journal.
                if (c == '%' || c == '|' || c == '\n' || c == '\r')
                {
                    _sb.Append('%').Append(((int)c).ToString("X2", CultureInfo.InvariantCulture));
                }
                else
                {
                    _sb.Append(c);
                }
            }
        }

        public void Int(int v)
        {
            Separator();
            _sb.Append(v.ToString(CultureInfo.InvariantCulture));
        }

        public void Float(float v)
        {
            Separator();
            _sb.Append(v.ToString("R", CultureInfo.InvariantCulture));
        }

        public void Decor(PlacedDecor d)
        {
            Int(d.uid);
            Str(d.item);
            Int(d.variant);
            Float(d.x);
            Float(d.y);
            Float(d.z);
            Float(d.yaw);
            Float(d.scale);
            Int((int)d.surface);
            Float(d.ex);
            Float(d.ey);
            Float(d.ez);
            Float(d.sag);
        }

        public override string ToString() => _sb.ToString();

        private void Separator()
        {
            if (_sb.Length > 0)
            {
                _sb.Append('|');
            }
        }
    }

    public sealed class CommandReader
    {
        private readonly string[] _fields;
        private int _next;

        public CommandReader(string text) => _fields = (text ?? string.Empty).Split('|');

        public bool AtEnd => _next == _fields.Length;

        public string Str()
        {
            string raw = Next();
            if (raw.IndexOf('%') < 0)
            {
                return raw;
            }

            var sb = new StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] == '%')
                {
                    if (i + 2 >= raw.Length)
                    {
                        throw new FormatException("bad escape");
                    }

                    sb.Append((char)int.Parse(raw.Substring(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    i += 2;
                }
                else
                {
                    sb.Append(raw[i]);
                }
            }

            return sb.ToString();
        }

        public int Int() => int.Parse(Next(), NumberStyles.Integer, CultureInfo.InvariantCulture);

        /// <summary>A list length: at most 4096, so a damaged line cannot ask for a huge allocation.</summary>
        public int Count()
        {
            int n = Int();
            if (n < 0 || n > 4096)
            {
                throw new FormatException("bad count");
            }

            return n;
        }

        public float Float() => float.Parse(Next(), NumberStyles.Float, CultureInfo.InvariantCulture);

        public PlacedDecor Decor() => new PlacedDecor
        {
            uid = Int(),
            item = Str(),
            variant = Int(),
            x = Float(),
            y = Float(),
            z = Float(),
            yaw = Float(),
            scale = Float(),
            surface = (Surface)Int(),
            ex = Float(),
            ey = Float(),
            ez = Float(),
            sag = Float(),
        };

        private string Next()
        {
            if (_next >= _fields.Length)
            {
                throw new FormatException("too few fields");
            }

            return _fields[_next++];
        }
    }

    /// <summary>PRD 9.1: 50-step undo and redo. A new edit clears the redo list; the oldest step falls off past the limit.</summary>
    public sealed class EditHistory
    {
        public const int DefaultCapacity = 50;

        private readonly List<IHouseCommand> _undo = new List<IHouseCommand>();
        private readonly List<IHouseCommand> _redo = new List<IHouseCommand>();

        public EditHistory(int capacity = DefaultCapacity) => Capacity = capacity;

        public int Capacity { get; }

        public int UndoCount => _undo.Count;

        public int RedoCount => _redo.Count;

        public bool Do(IHouseCommand command, HouseDesign design)
        {
            if (!command.Apply(design))
            {
                return false;
            }

            _undo.Add(command);
            if (_undo.Count > Capacity)
            {
                _undo.RemoveAt(0);
            }

            _redo.Clear();
            return true;
        }

        public bool Undo(HouseDesign design)
        {
            if (_undo.Count == 0)
            {
                return false;
            }

            IHouseCommand command = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            command.Revert(design);
            _redo.Add(command);
            return true;
        }

        public bool Redo(HouseDesign design)
        {
            if (_redo.Count == 0)
            {
                return false;
            }

            IHouseCommand command = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);
            if (!command.Apply(design))
            {
                return false;
            }

            _undo.Add(command);
            return true;
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
        }
    }
}
