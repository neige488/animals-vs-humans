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
        public string CharacterId, CharacterName, CharacterRarity;
        public bool IsBot;
        public int Ammo;
        public double ReloadRemaining, AttackGraceRemaining, FireCooldownRemaining;
        // Impact timers (host rules): attack tell, presented swing, hit-stop freeze and bubble stun.
        public double AttackWindupRemaining, SwingRemaining, HitStopRemaining, StunRemaining;
        // The workshop tool a human carries this round, and how long its last use is still shown (punch out, gust).
        public ToolKind Tool;
        public double ToolUseRemaining;
        public ActionPhase Action;
        // Special ability (Abilities): its wait, the uses left this round (limited abilities) and how long its own effect still runs.
        public AbilityKind Ability;
        public double AbilityCooldownRemaining, AbilityActiveRemaining;
        public int AbilityUsesRemaining;
        /// <summary>Heading (degrees) of a straight-line ability in progress (charge, kickboard).</summary>
        public float AbilityYaw;
        // Effects other characters put on this one: slowed, held in place (and by what), lifted in a bubble, shaken view,
        // immune to infection, floating in low gravity.
        public double SlowRemaining, RootRemaining, LiftRemaining, ShakeRemaining, InvulnerableRemaining, LowGravityRemaining;
        public AbilityKind Hold;
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
        /// <summary>Recent public impact events (bounded window), oldest first.</summary>
        public FeelEvent[] Events;
        /// <summary>Map of the current round (GameMaps). Changes only at a round start.</summary>
        public int Map;
        /// <summary>Things abilities left in the world: banana peels, spring pads, scarecrows, smoke, a zero-gravity zone.</summary>
        public GadgetState[] Gadgets;
    }
    public sealed partial class PlaytestSession
    {
        private PlayerState[] players = Array.Empty<PlayerState>();
        private readonly Random random, toolRandom, characterRandom;
        private RoundPhase phase;
        private double remaining, hostTime, phaseDeadline, phaseStartedAt;
        private int round, map;
        private Faction? winner;
        private CharacterDefinition[] humanRoster,animalRoster;
        private int nextAnimal;
        private BirthNotice[] births = Array.Empty<BirthNotice>();
        public PlaytestSession(int randomSeed, string settingsPath = null) { random = new Random(randomSeed); toolRandom = new Random(randomSeed * 31 + 7); characterRandom = new Random(randomSeed * 17 + 3); settings.Load(settingsPath); }
        /// <param name="map">Map for this run from the first round (start screen choice); null keeps the loaded settings' map.</param>
        public void StartSolo(string nickname, string animalKind = "임시 동물", string animalRarity = "일반", CharacterDefinition[] humans=null, CharacterDefinition[] animals=null, int? map=null)
        {
            if (string.IsNullOrWhiteSpace(nickname) || nickname.Trim().Length > 20) throw new ArgumentException("닉네임은 1~20자입니다.");
            humanRoster=Roster(humans,new CharacterDefinition("human-default","인간"));
            animalRoster=Roster(animals,new CharacterDefinition("animal-default",animalKind,animalRarity));
            ResetParticipants();
            if (map.HasValue) settings.Current.Map = GameMaps.Clamp(map.Value);
            round = 0; hostTime = 0;
            players = Enumerable.Range(0, 12).Select(i => new PlayerState {
                Slot = i, Nickname = i == 0 ? nickname : "봇 " + i, IsBot = i != 0,
                Faction = Faction.Human, Position = Spawn(i)
            }).ToArray();
            BeginRound();
        }
        private void BeginRound() {
            settings.BeginRound(); map = GameMaps.Clamp(settings.Current.Map); round++; phase = RoundPhase.Preparation; remaining = settings.Current.PreparationSeconds; winner = null;
            births = Array.Empty<BirthNotice>();
            nextAnimal=(round-1)*FirstAnimals;
            foreach (var player in players) { player.Faction = Faction.Human; player.CharacterId = null; }
            foreach (var player in players) { AssignCharacter(player,settings.Current.RandomCharacters?Draw(humanRoster,Faction.Human):humanRoster[player.Slot%humanRoster.Length]);player.Position = Spawn(player.Slot);
                int tool = settings.Current.Tool; player.Tool = (ToolKind)(tool >= 0 && tool < Tools.Count ? tool : toolRandom.Next(Tools.Count)); }
            ResetCombat(); phaseStartedAt=hostTime; phaseDeadline=hostTime+remaining;
        }
        public void Advance(double elapsed) {
            if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0) throw new ArgumentOutOfRangeException(nameof(elapsed));
            if (players.Length == 0) throw new InvalidOperationException("세션을 먼저 시작하세요.");
            double targetTime=hostTime+elapsed;
            while (targetTime >= phaseDeadline) {
                AdvanceCombat(phaseDeadline-hostTime);
                hostTime=phaseDeadline;
                if (phase == RoundPhase.Preparation) {
                    var slots = Enumerable.Range(0, players.Length).ToArray();
                    for (int i = 0; i < FirstAnimals; i++) {
                        int selected = random.Next(i, slots.Length);
                        int swap = slots[i]; slots[i] = slots[selected]; slots[selected] = swap;
                        players[slots[i]].Faction = Faction.Animal;
                        AssignCharacter(players[slots[i]],NextAnimal());
                        players[slots[i]].AttackGraceRemaining = settings.Current.InitialAttackGrace;
                    }
                    if (GameMaps.AnimalsStartApart(map)) for (int i = 0; i < FirstAnimals; i++) players[slots[i]].Position = GameMaps.AnimalStart(map, i);
                    var born=players.Where(p=>p.Faction==Faction.Animal).ToArray();
                    PublishBirth(born);foreach(var p in born)Publish(FeelEventKind.Transform,-1,p.Slot,p.Position);
                    phase = RoundPhase.Chase; remaining = GameMaps.ChaseSeconds(map,settings.Current.RoundSeconds);
                } else if (phase == RoundPhase.Chase) {
                    phase = RoundPhase.Results; remaining = settings.Current.ResultSeconds; winner = Faction.Human; ClearActions();
                } else BeginRound();
                phaseStartedAt=hostTime; phaseDeadline=hostTime+remaining;
            }
            AdvanceCombat(targetTime-hostTime);
            hostTime=targetTime;
            remaining=phaseDeadline-hostTime;
        }
        // Called only by the authoritative physics adapter, never by remote participant messages.
        static CharacterDefinition[] Roster(CharacterDefinition[] source,CharacterDefinition fallback) {
            var roster=source==null?new[]{fallback}:source.ToArray();
            if(roster.Length==0||roster.Length>64||roster.Any(c=>c==null)||roster.Select(c=>c.Id).Distinct(StringComparer.Ordinal).Count()!=roster.Length)throw new ArgumentException("캐릭터 목록이 올바르지 않습니다.");
            return roster;
        }
        int FirstAnimals=>GameMaps.FirstAnimals(map,settings.Current.InitialAnimals,players.Length);
        CharacterDefinition NextAnimal()=>settings.Current.RandomCharacters?Draw(animalRoster,Faction.Animal):animalRoster[nextAnimal++%animalRoster.Length];
        /// <summary>
        /// Random character: first the grade by <see cref="Abilities.Odds"/> (over the grades this roster has), then one of that
        /// grade. Within the grade a character nobody on that side plays yet is preferred, so the grade odds stay exact.
        /// </summary>
        CharacterDefinition Draw(CharacterDefinition[] roster,Faction faction) {
            int total=0,grade=0;for(int g=0;g<Abilities.Grades.Length;g++)if(roster.Any(c=>Abilities.Grade(c.Rarity)==g))total+=Abilities.Odds[g];
            int roll=characterRandom.Next(Math.Max(1,total));
            for(int g=0;g<Abilities.Grades.Length;g++){if(!roster.Any(c=>Abilities.Grade(c.Rarity)==g))continue;grade=g;if(roll<Abilities.Odds[g])break;roll-=Abilities.Odds[g];}
            var pool=roster.Where(c=>Abilities.Grade(c.Rarity)==grade).ToArray();
            var fresh=pool.Where(c=>!players.Any(p=>p.Faction==faction&&p.CharacterId==c.Id)).ToArray();
            if(fresh.Length>0)pool=fresh;
            return pool[characterRandom.Next(pool.Length)];
        }
        void AssignCharacter(PlayerState player,CharacterDefinition character){player.CharacterId=character.Id;player.CharacterName=character.Name;player.CharacterRarity=character.Rarity;ResetAbility(player);}
        /// <summary>Authoritative simulation clock in seconds since the session started (frame timestamps).</summary>
        public double HostTime => hostTime;
        /// <summary>Current round number (frame tagging without copying the whole state).</summary>
        public int Round => round;
        public void RecordWorldPosition(int slot, WorldPosition position) {
            if (slot < 0 || slot >= players.Length) throw new ArgumentOutOfRangeException(nameof(slot));
            players[slot].Position = position;
        }
        private WorldPosition Spawn(int slot) => GameMaps.Spawn(map, slot);
        /// <summary>Map of the current round.</summary>
        public int Map => map;
        public SessionState Observe() => new SessionState {
            Players = players.Select(p => {var copy=p.Copy();copy.Action=ActionOf(p);return copy;}).ToArray(), Phase = phase, SecondsRemaining = remaining, Round = round, Map = map, Winner = winner,
            BirthSecondsRemaining = birthSecondsRemaining,
            Births = births.Select(b => new BirthNotice { Kind = b.Kind, Rarity = b.Rarity, Count = b.Count }).ToArray(),
            Events = events.Select(e => e.Copy()).ToArray(),
            Gadgets = gadgets.Select(g => g.Copy()).ToArray()
        };
    }
}
