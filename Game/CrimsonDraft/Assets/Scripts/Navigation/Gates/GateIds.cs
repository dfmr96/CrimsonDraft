#nullable enable

namespace CrimsonDraft.Navigation.Gates
{
    // Generic: one-shot things with no state of their own (beeper receivers...), kept in GateRegistry.
    public enum GateKind { Socket, Puzzle, Radio, Generic }

    /// <summary>
    /// Gate ids are the underlying object's own id (socketId, puzzleId, radio node name) prefixed with
    /// its kind, so ids from different registries can never collide: "socket:x", "puzzle:x", "radio:x", "gate:x".
    /// </summary>
    public static class GateIds
    {
        private const string SocketPrefix  = "socket:";
        private const string PuzzlePrefix  = "puzzle:";
        private const string RadioPrefix   = "radio:";
        private const string GenericPrefix = "gate:";

        public static string Socket(string socketId) => SocketPrefix + socketId;
        public static string Puzzle(string puzzleId) => PuzzlePrefix + puzzleId;
        public static string Radio(string nodeName)  => RadioPrefix + nodeName;
        public static string Generic(string gateId)  => GenericPrefix + gateId;

        /// <summary>False for an unknown prefix or an empty id after it.</summary>
        public static bool TryParse(string? gateId, out GateKind kind, out string id)
        {
            kind = default;
            id   = string.Empty;
            if (string.IsNullOrEmpty(gateId)) return false;

            if      (gateId.StartsWith(SocketPrefix))  { kind = GateKind.Socket;  id = gateId.Substring(SocketPrefix.Length); }
            else if (gateId.StartsWith(PuzzlePrefix))  { kind = GateKind.Puzzle;  id = gateId.Substring(PuzzlePrefix.Length); }
            else if (gateId.StartsWith(RadioPrefix))   { kind = GateKind.Radio;   id = gateId.Substring(RadioPrefix.Length); }
            else if (gateId.StartsWith(GenericPrefix)) { kind = GateKind.Generic; id = gateId.Substring(GenericPrefix.Length); }
            else return false;

            return id.Length > 0;
        }
    }
}
