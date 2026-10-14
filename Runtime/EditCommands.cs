using System.Collections.Generic;
using UnityEngine;

namespace ITKamianets.Engine.Interaction
{
    /// <summary>One undoable change to the scene.</summary>
    public interface IEditCommand
    {
        string Name { get; }
        void Do();
        void Undo();
    }

    /// <summary>Undo and redo history. A new change clears what could be redone.</summary>
    public class UndoHistory
    {
        private readonly List<IEditCommand> _done = new List<IEditCommand>();
        private readonly List<IEditCommand> _undone = new List<IEditCommand>();

        public int Limit { get; set; } = 200;

        public bool CanUndo => _done.Count > 0;
        public bool CanRedo => _undone.Count > 0;

        /// <summary>Runs a command and remembers it.</summary>
        public void Run(IEditCommand command)
        {
            command.Do();
            Record(command);
        }

        /// <summary>Remembers a command whose change is already applied (e.g. a finished drag).</summary>
        public void Record(IEditCommand command)
        {
            _done.Add(command);
            if (_done.Count > Limit)
            {
                _done.RemoveAt(0);
            }

            _undone.Clear();
        }

        public IEditCommand Undo()
        {
            if (!CanUndo)
            {
                return null;
            }

            var command = _done[_done.Count - 1];
            _done.RemoveAt(_done.Count - 1);
            command.Undo();
            _undone.Add(command);
            return command;
        }

        public IEditCommand Redo()
        {
            if (!CanRedo)
            {
                return null;
            }

            var command = _undone[_undone.Count - 1];
            _undone.RemoveAt(_undone.Count - 1);
            command.Do();
            _done.Add(command);
            return command;
        }

        public void Clear()
        {
            _done.Clear();
            _undone.Clear();
        }
    }

    /// <summary>A transform as plain values, for before/after pairs.</summary>
    public struct TransformState
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;

        public static TransformState Of(Transform transform)
        {
            return new TransformState { Position = transform.position, Rotation = transform.rotation, Scale = transform.localScale };
        }

        public void ApplyTo(Transform transform)
        {
            transform.SetPositionAndRotation(Position, Rotation);
            transform.localScale = Scale;
        }
    }

    /// <summary>Moves, turns or scales objects from one set of transforms to another.</summary>
    public class TransformCommand : IEditCommand
    {
        private readonly Transform[] _targets;
        private readonly TransformState[] _before;
        private readonly TransformState[] _after;

        public string Name { get; }

        public TransformCommand(string name, Transform[] targets, TransformState[] before, TransformState[] after)
        {
            Name = name;
            _targets = targets;
            _before = before;
            _after = after;
        }

        public void Do()
        {
            for (var i = 0; i < _targets.Length; i++)
            {
                if (_targets[i] != null)
                {
                    _after[i].ApplyTo(_targets[i]);
                }
            }
        }

        public void Undo()
        {
            for (var i = 0; i < _targets.Length; i++)
            {
                if (_targets[i] != null)
                {
                    _before[i].ApplyTo(_targets[i]);
                }
            }
        }
    }

    /// <summary>
    /// Shows or hides objects: delete hides them (so undo can bring them back), duplicate shows
    /// new copies (so undo can hide them). Hidden objects are left out of <c>SceneSaver</c> captures.
    /// </summary>
    public class ActivateCommand : IEditCommand
    {
        private readonly GameObject[] _objects;
        private readonly bool _active;

        public string Name { get; }

        public ActivateCommand(string name, GameObject[] objects, bool active)
        {
            Name = name;
            _objects = objects;
            _active = active;
        }

        public void Do()
        {
            Set(_active);
        }

        public void Undo()
        {
            Set(!_active);
        }

        private void Set(bool active)
        {
            foreach (var go in _objects)
            {
                if (go != null)
                {
                    go.SetActive(active);
                }
            }
        }
    }
}
