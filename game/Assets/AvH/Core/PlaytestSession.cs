using System;
using System.Linq;
namespace AvH
{
    public enum Faction { Human, Animal }
    public enum RoundPhase { Preparation, Chase, Results }
    [Serializable] public struct WorldPosition
    {
        public float X, Y, Z;
        public WorldPosition(float x, float y, float z) { X = x; Y = y; Z = z; }
    }
    [Serializable] public sealed class PlayerState
    {
        public int Slot;
        public string Nickname;
        public bool IsBot;
        public Faction Faction;
        public WorldPosition Position;
        public PlayerState Copy() => (PlayerState)MemberwiseClone();
    }
    public sealed class SessionState
    {
        public PlayerState[] Players;
        public RoundPhase Phase;
        public double SecondsRemaining;
        public int Round;
    }
    public sealed class PlaytestSession
    {
        private PlayerState[] players = Array.Empty<PlayerState>();
        public PlaytestSession(int randomSeed) { }
        public void StartSolo(string nickname)
        {
            players = Enumerable.Range(0, 12).Select(i => new PlayerState {
                Slot = i, Nickname = i == 0 ? nickname : "봇 " + i, IsBot = i != 0,
                Faction = Faction.Human, Position = Spawn(i)
            }).ToArray();
        }
        private static WorldPosition Spawn(int slot) => new WorldPosition((slot % 4 - 1.5f) * 3, 1, (slot / 4 - 1) * 3);
        public SessionState Observe() => new SessionState {
            Players = players.Select(p => p.Copy()).ToArray(), Phase = RoundPhase.Preparation, SecondsRemaining = 20, Round = 1
        };
    }
}
