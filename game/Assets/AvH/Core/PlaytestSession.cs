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
        public int Ammo;
        public double ReloadRemaining, AttackGraceRemaining, FireCooldownRemaining;
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
        public double BirthSecondsRemaining;
    }
    public sealed partial class PlaytestSession
    {
        private PlayerState[] players = Array.Empty<PlayerState>();
        private readonly Random random;
        private RoundPhase phase;
        private double remaining;
        private int round;
        private Faction? winner;
        private string animalKind, animalRarity;
        private BirthNotice[] births = Array.Empty<BirthNotice>();
        public PlaytestSession(int randomSeed) { random = new Random(randomSeed); }
        public void StartSolo(string nickname, string animalKind = "임시 동물", string animalRarity = "일반")
        {
            if (string.IsNullOrWhiteSpace(nickname) || nickname.Trim().Length > 20) throw new ArgumentException("닉네임은 1~20자입니다.");
            this.animalKind = animalKind; this.animalRarity = animalRarity;
            round = 0;
            players = Enumerable.Range(0, 12).Select(i => new PlayerState {
                Slot = i, Nickname = i == 0 ? nickname : "봇 " + i, IsBot = i != 0,
                Faction = Faction.Human, Position = Spawn(i)
            }).ToArray();
            BeginRound();
        }
        private void BeginRound() {
            settings.BeginRound(); round++; phase = RoundPhase.Preparation; remaining = settings.Current.PreparationSeconds; winner = null;
            births = Array.Empty<BirthNotice>();
            foreach (var player in players) { player.Faction = Faction.Human; player.Position = Spawn(player.Slot); }
            ResetCombat();
        }
        public void Advance(double elapsed) {
            if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0) throw new ArgumentOutOfRangeException(nameof(elapsed));
            if (players.Length == 0) throw new InvalidOperationException("세션을 먼저 시작하세요.");
            while (elapsed + 1e-9 >= remaining) {
                AdvanceCombat(remaining);
                elapsed = Math.Max(0, elapsed - remaining);
                if (phase == RoundPhase.Preparation) {
                    var slots = Enumerable.Range(0, players.Length).ToArray();
                    for (int i = 0; i < settings.Current.InitialAnimals; i++) {
                        int selected = random.Next(i, slots.Length);
                        int swap = slots[i]; slots[i] = slots[selected]; slots[selected] = swap;
                        players[slots[i]].Faction = Faction.Animal;
                        players[slots[i]].AttackGraceRemaining = settings.Current.InitialAttackGrace;
                    }
                    PublishBirth(settings.Current.InitialAnimals);
                    phase = RoundPhase.Chase; remaining = settings.Current.RoundSeconds;
                } else if (phase == RoundPhase.Chase) {
                    phase = RoundPhase.Results; remaining = 5; winner = Faction.Human;
                } else BeginRound();
            }
            AdvanceCombat(elapsed);
            remaining -= elapsed;
        }
        // Called only by the authoritative physics adapter, never by remote participant messages.
        public void RecordWorldPosition(int slot, WorldPosition position) {
            if (slot < 0 || slot >= players.Length) throw new ArgumentOutOfRangeException(nameof(slot));
            players[slot].Position = position;
        }
        private static WorldPosition Spawn(int slot) => new WorldPosition((slot % 4 - 1.5f) * 3, 1, (slot / 4 - 1) * 3);
        public SessionState Observe() => new SessionState {
            Players = players.Select(p => p.Copy()).ToArray(), Phase = phase, SecondsRemaining = remaining, Round = round, Winner = winner,
            BirthSecondsRemaining = birthSecondsRemaining,
            Births = births.Select(b => new BirthNotice { Kind = b.Kind, Rarity = b.Rarity, Count = b.Count }).ToArray()
        };
    }
}
