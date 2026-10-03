using Paniq.Simulation;

namespace Paniq.Presentation
{
    /// <summary>
    /// The words for what the player's hand asks (2026-10-02, the owner: "I
    /// don't really feel that the interactions are clear ... Maybe they are
    /// doing it, but I'm not sure"). One table, so the ring under the hand,
    /// the sign over whoever takes it up, the line under the pointer and the
    /// Tab panel all say the same thing in the same two or three words. The
    /// run decides which ask it is (<see cref="InfluenceSystem.AskAt"/>);
    /// this only names it.
    /// </summary>
    internal static class HandAskWords
    {
        /// <summary>What the hand asks, as written at its ring: "clear the boxes". Null for no ask.</summary>
        public static string Label(HandAsk ask)
        {
            switch (ask)
            {
                case HandAsk.ComeHere: return "come here";
                case HandAsk.AwayFromHere: return "away from here";
                case HandAsk.OpenTheDoor: return "open the door";
                case HandAsk.ShutTheDoor: return "shut the door";
                case HandAsk.PoundTheDoor: return "pound the door";
                case HandAsk.ClearTheBoxes: return "clear the boxes";
                case HandAsk.CarryItOff: return "carry it off";
                case HandAsk.TakeTheBottle: return "take the bottle";
                case HandAsk.SitHere: return "sit here";
                case HandAsk.PullTheAlarm: return "pull the alarm";
                case HandAsk.GetTheCard: return "get the card";
                default: return null;
            }
        }

        /// <summary>What somebody who took it up is doing, as written over their head: "clearing the boxes...".</summary>
        public static string Doing(HandAsk ask)
        {
            switch (ask)
            {
                case HandAsk.ComeHere: return "coming over...";
                case HandAsk.AwayFromHere: return "moving off...";
                case HandAsk.OpenTheDoor: return "opening the door...";
                case HandAsk.ShutTheDoor: return "shutting the door...";
                case HandAsk.PoundTheDoor: return "pounding the door...";
                case HandAsk.ClearTheBoxes: return "clearing the boxes...";
                case HandAsk.CarryItOff: return "carrying it off...";
                case HandAsk.TakeTheBottle: return "taking the bottle...";
                case HandAsk.SitHere: return "sitting down...";
                case HandAsk.PullTheAlarm: return "going for the alarm...";
                case HandAsk.GetTheCard: return "going for the card...";
                default: return "answering your hand...";
            }
        }

        /// <summary>What is written at the place when it is done: "opened!", "cleared!".</summary>
        public static string Done(HandAsk ask)
        {
            switch (ask)
            {
                case HandAsk.OpenTheDoor: return "opened!";
                case HandAsk.ShutTheDoor: return "shut!";
                case HandAsk.ClearTheBoxes: return "cleared!";
                case HandAsk.CarryItOff: return "taken!";
                case HandAsk.TakeTheBottle: return "taken!";
                case HandAsk.SitHere: return "sat down!";
                case HandAsk.PullTheAlarm: return "pulled!";
                default: return "done!";
            }
        }
    }
}
