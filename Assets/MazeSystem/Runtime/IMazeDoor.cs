using System;

namespace MazeSystem
{
    /// <summary>
    /// Minimal contract the maze transition needs from a main entrance door.
    /// Allows the project's own door script to be wrapped later without changing the manager.
    /// </summary>
    public interface IMazeDoor
    {
        bool IsOpen { get; }
        bool IsClosed { get; }
        bool IsMoving { get; }
        bool IsOccupied { get; }

        event Action OnOpened;
        event Action OnClosed;

        void Open();
        void Close();
        void SetStateInstant(bool open);
    }
}
