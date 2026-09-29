using System;
using System.Collections.Generic;
using System.Linq;
namespace AvH {
 public enum SlotClaimOutcome { Claimed, WaitingForRound, Full, Duplicate, Invalid }
 public sealed partial class PlaytestSession {
  readonly object participantGate=new object();
  readonly Dictionary<string,int> owners=new Dictionary<string,int>(StringComparer.Ordinal);
  readonly Dictionary<string,Tuple<int,int>> previousSlots=new Dictionary<string,Tuple<int,int>>(StringComparer.Ordinal);
  int rememberedParticipantRound;
  void ResetParticipants(){lock(participantGate){owners.Clear();previousSlots.Clear();rememberedParticipantRound=0;}}
  void ForgetPreviousRounds(){if(rememberedParticipantRound!=round){previousSlots.Clear();rememberedParticipantRound=round;}}
  public bool TryClaimSlot(string identity,string nickname,out int slot)=>ClaimSlot(identity,nickname,out slot)==SlotClaimOutcome.Claimed;
  public SlotClaimOutcome ClaimSlot(string identity,string nickname,out int slot) {
   lock(participantGate){
    slot=-1;ForgetPreviousRounds();
    if(identity==null||identity.Length!=32||string.IsNullOrWhiteSpace(nickname)||nickname.Length>20)return SlotClaimOutcome.Invalid;
    if(owners.ContainsKey(identity))return SlotClaimOutcome.Duplicate;
    if(!players.Any(p=>p.IsBot))return SlotClaimOutcome.Full;
    if(phase==RoundPhase.Results)return SlotClaimOutcome.WaitingForRound;
    PlayerState chosen=null;Tuple<int,int> old;
    if(previousSlots.TryGetValue(identity,out old)&&old.Item2==round)chosen=players.FirstOrDefault(p=>p.Slot==old.Item1&&p.IsBot);
    chosen=chosen??players.Where(p=>p.IsBot).OrderBy(p=>p.Faction==Faction.Human?0:1).ThenBy(p=>p.Slot).First();
    chosen.IsBot=false;chosen.Nickname=nickname;slot=chosen.Slot;owners.Add(identity,slot);return SlotClaimOutcome.Claimed;
   }
  }
  public bool ReleaseSlotToBot(int slot,string identity) {
   lock(participantGate){int owned;ForgetPreviousRounds();
    if(identity==null||!owners.TryGetValue(identity,out owned)||owned!=slot)return false;
    owners.Remove(identity);players[slot].IsBot=true;players[slot].Nickname="봇 "+slot;
    if(previousSlots.Count<1024||previousSlots.ContainsKey(identity))previousSlots[identity]=Tuple.Create(slot,round);
    return true;
   }
  }
 }
}
