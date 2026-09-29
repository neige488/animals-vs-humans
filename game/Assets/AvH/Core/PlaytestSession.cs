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
    public sealed class BirthNotice { public string Kind; public string Rarity; public int Count; }
    public sealed class SessionState
    {
        public PlayerState[] Players;
        public RoundPhase Phase;
        public double SecondsRemaining;
        public int Round;
        public Faction? Winner;
        public BirthNotice[] Births;
    }
    public sealed class PlaytestSession
    {
        private PlayerState[] players = Array.Empty<PlayerState>();
        private readonly Random random;
        private RoundPhase phase;
        private double remaining;
        private int round;
        private Faction? winner;
        private BirthNotice[] births = Array.Empty<BirthNotice>();
        public PlaytestSession(int randomSeed) { random = new Random(randomSeed); }
        public void StartSolo(string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname) || nickname.Trim().Length > 20) throw new ArgumentException("닉네임은 1~20자입니다.");
            round = 0;
            players = Enumerable.Range(0, 12).Select(i => new PlayerState {
                Slot = i, Nickname = i == 0 ? nickname : "봇 " + i, IsBot = i != 0,
                Faction = Faction.Human, Position = Spawn(i)
            }).ToArray();
            BeginRound();
        }
        private void BeginRound() {
            round++; phase = RoundPhase.Preparation; remaining = 20; winner = null;
            births = Array.Empty<BirthNotice>();
            foreach (var player in players) { player.Faction = Faction.Human; player.Position = Spawn(player.Slot); }
        }
        public void Advance(double elapsed) {
            if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0) throw new ArgumentOutOfRangeException(nameof(elapsed));
            if (players.Length == 0) throw new InvalidOperationException("세션을 먼저 시작하세요.");
            while (elapsed + 1e-9 >= remaining) {
                elapsed = Math.Max(0, elapsed - remaining);
                if (phase == RoundPhase.Preparation) {
                    var slots = Enumerable.Range(0, players.Length).ToArray();
                    for (int i = 0; i < 2; i++) {
                        int selected = random.Next(i, slots.Length);
                        int swap = slots[i]; slots[i] = slots[selected]; slots[selected] = swap;
                        players[slots[i]].Faction = Faction.Animal;
                    }
                    births = new[] { new BirthNotice { Kind = "동물", Rarity = "일반", Count = 2 } };
                    phase = RoundPhase.Chase; remaining = 180;
                } else if (phase == RoundPhase.Chase) {
                    phase = RoundPhase.Results; remaining = 5; winner = Faction.Human;
                } else BeginRound();
            }
            remaining -= elapsed;
        }
        private static WorldPosition Spawn(int slot) => new WorldPosition((slot % 4 - 1.5f) * 3, 1, (slot / 4 - 1) * 3);
        public SessionState Observe() => new SessionState {
            Players = players.Select(p => p.Copy()).ToArray(), Phase = phase, SecondsRemaining = remaining, Round = round, Winner = winner,
            Births = births.Select(b => new BirthNotice { Kind = b.Kind, Rarity = b.Rarity, Count = b.Count }).ToArray()
        };
    }
}
