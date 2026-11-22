using System;
using System.Collections.Generic;
using Cricket.Simulation.Core.DataModels;
using Cricket.Simulation.Core.Events;

namespace Cricket.Simulation.Core.PresentationContracts
{
    public enum BroadcastCueCategory
    {
        CameraCut = 0,
        ReplayPackage = 1,
        CrowdReaction = 2,
        CeremonySequence = 3,
        CommentaryTrigger = 4,
        HudGraphic = 5
    }

    public enum BroadcastPriority
    {
        Ambient = 0,
        Normal = 1,
        High = 2,
        ImmediateInterrupt = 3
    }

    /// <summary>
    /// Immutable directive emitted by the Broadcast & Match Director to presentation systems
    /// (cameras, replays, crowd audio, ceremonies, commentary, scorebug).
    /// Completely decoupled from official cricket rules and scoring state.
    /// </summary>
    [Serializable]
    public readonly struct BroadcastCue
    {
        public long SourceEventSequence { get; }
        public BroadcastCueCategory Category { get; }
        public BroadcastPriority Priority { get; }
        public string CueKey { get; }
        public string ContextPayload { get; }

        public BroadcastCue(
            long sourceEventSequence,
            BroadcastCueCategory category,
            BroadcastPriority priority,
            string cueKey,
            string contextPayload = "")
        {
            SourceEventSequence = sourceEventSequence;
            Category = category;
            Priority = priority;
            CueKey = cueKey ?? string.Empty;
            ContextPayload = contextPayload ?? string.Empty;
        }
    }

    /// <summary>
    /// Dedicated Broadcast & Match Director contract. Coordinates camera cuts, replays,
    /// crowd reactions, ceremonies, commentary timing, and match presentation strictly from
    /// simulation events without ever modifying rules or score state.
    /// </summary>
    public interface IBroadcastDirector : IDisposable
    {
        event Action<BroadcastCue>? CueEmitted;
        IReadOnlyList<BroadcastCue> EmittedCueHistory { get; }
        void ClearHistory();
    }

    /// <summary>
    /// Pure C# event-driven Broadcast & Match Director coordinator.
    /// Unity presentation adapters subscribe to <see cref="CueEmitted"/> in later milestones.
    /// </summary>
    public sealed class BroadcastMatchDirector : IBroadcastDirector
    {
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>(8);
        private readonly List<BroadcastCue> _cueHistory = new List<BroadcastCue>(64);

        public event Action<BroadcastCue>? CueEmitted;
        public IReadOnlyList<BroadcastCue> EmittedCueHistory => _cueHistory;

        public BroadcastMatchDirector(IMatchEventBus eventBus)
        {
            if (eventBus == null)
            {
                throw new ArgumentNullException(nameof(eventBus));
            }

            _subscriptions.Add(eventBus.Subscribe<MatchPhaseChangedEvent>(OnMatchPhaseChanged));
            _subscriptions.Add(eventBus.Subscribe<TossCompletedEvent>(OnTossCompleted));
            _subscriptions.Add(eventBus.Subscribe<BallResolvedEvent>(OnBallResolved));
            _subscriptions.Add(eventBus.Subscribe<WicketFallenEvent>(OnWicketFallen));
            _subscriptions.Add(eventBus.Subscribe<OverCompletedEvent>(OnOverCompleted));
            _subscriptions.Add(eventBus.Subscribe<InningsCompletedEvent>(OnInningsCompleted));
            _subscriptions.Add(eventBus.Subscribe<MatchCompletedEvent>(OnMatchCompleted));
        }

        public void ClearHistory()
        {
            _cueHistory.Clear();
        }

        public void Dispose()
        {
            for (int i = 0; i < _subscriptions.Count; i++)
            {
                _subscriptions[i].Dispose();
            }

            _subscriptions.Clear();
        }

        private void OnMatchPhaseChanged(MatchPhaseChangedEvent evt)
        {
            switch (evt.CurrentPhase)
            {
                case MatchPhase.PreMatchCeremony:
                    Emit(evt.SequenceNumber, BroadcastCueCategory.CeremonySequence, BroadcastPriority.High, "Ceremony.PreMatchIntro", evt.Reason);
                    Emit(evt.SequenceNumber, BroadcastCueCategory.CameraCut, BroadcastPriority.Normal, "Cam.StadiumEstablishing");
                    break;
                case MatchPhase.Toss:
                    Emit(evt.SequenceNumber, BroadcastCueCategory.CeremonySequence, BroadcastPriority.High, "Ceremony.CoinToss");
                    Emit(evt.SequenceNumber, BroadcastCueCategory.CameraCut, BroadcastPriority.High, "Cam.PitchCenterToss");
                    break;
                case MatchPhase.OverSetup:
                    Emit(evt.SequenceNumber, BroadcastCueCategory.CameraCut, BroadcastPriority.Normal, "Cam.BroadcastBatting");
                    break;
                case MatchPhase.PostMatchCeremony:
                    Emit(evt.SequenceNumber, BroadcastCueCategory.CeremonySequence, BroadcastPriority.High, "Ceremony.PresentationAndTrophy");
                    break;
            }
        }

        private void OnTossCompleted(TossCompletedEvent evt)
        {
            Emit(evt.SequenceNumber, BroadcastCueCategory.HudGraphic, BroadcastPriority.High, "Hud.TossBanner", $"{evt.TossWinnerTeamId}:{evt.Decision}");
            Emit(evt.SequenceNumber, BroadcastCueCategory.CommentaryTrigger, BroadcastPriority.High, "Commentary.TossResult", evt.BattingFirstTeamId);
        }

        private void OnBallResolved(BallResolvedEvent evt)
        {
            BallEventRecord record = evt.Record;

            if (record.IsBoundarySix)
            {
                Emit(evt.SequenceNumber, BroadcastCueCategory.CrowdReaction, BroadcastPriority.ImmediateInterrupt, "Crowd.RoarSix");
                Emit(evt.SequenceNumber, BroadcastCueCategory.CommentaryTrigger, BroadcastPriority.ImmediateInterrupt, "Commentary.Six", record.StrikerId);
                Emit(evt.SequenceNumber, BroadcastCueCategory.ReplayPackage, BroadcastPriority.High, "Replay.BoundarySix", record.SequenceId.ToString());
            }
            else if (record.IsBoundaryFour)
            {
                Emit(evt.SequenceNumber, BroadcastCueCategory.CrowdReaction, BroadcastPriority.High, "Crowd.CheerFour");
                Emit(evt.SequenceNumber, BroadcastCueCategory.CommentaryTrigger, BroadcastPriority.High, "Commentary.Four", record.StrikerId);
            }
            else if (record.PrimaryExtraType == ExtraType.NoBall)
            {
                Emit(evt.SequenceNumber, BroadcastCueCategory.HudGraphic, BroadcastPriority.High, "Hud.NoBallSiren", record.NoBallReason.ToString());
                Emit(evt.SequenceNumber, BroadcastCueCategory.CommentaryTrigger, BroadcastPriority.Normal, "Commentary.NoBall", record.NoBallReason.ToString());
            }
            else if (record.PrimaryExtraType == ExtraType.Wide)
            {
                Emit(evt.SequenceNumber, BroadcastCueCategory.CommentaryTrigger, BroadcastPriority.Normal, "Commentary.Wide");
            }
            else if (record.TotalRunsScored == 0 && record.Dismissal == null)
            {
                Emit(evt.SequenceNumber, BroadcastCueCategory.CommentaryTrigger, BroadcastPriority.Ambient, "Commentary.DotBall");
            }
        }

        private void OnWicketFallen(WicketFallenEvent evt)
        {
            Emit(evt.SequenceNumber, BroadcastCueCategory.CameraCut, BroadcastPriority.ImmediateInterrupt, "Cam.WicketCelebration", evt.Dismissal.Type.ToString());
            Emit(evt.SequenceNumber, BroadcastCueCategory.CrowdReaction, BroadcastPriority.ImmediateInterrupt, "Crowd.WicketEruption");
            Emit(evt.SequenceNumber, BroadcastCueCategory.CommentaryTrigger, BroadcastPriority.ImmediateInterrupt, "Commentary.Wicket", evt.Dismissal.OutBatterId);
            Emit(evt.SequenceNumber, BroadcastCueCategory.ReplayPackage, BroadcastPriority.High, "Replay.WicketSlowMo", evt.Dismissal.Type.ToString());
        }

        private void OnOverCompleted(OverCompletedEvent evt)
        {
            Emit(evt.SequenceNumber, BroadcastCueCategory.HudGraphic, BroadcastPriority.Normal, "Hud.OverSummary", $"Over {evt.CompletedOverNumber}:{evt.RunsConcededInOver}");
            if (evt.IsMaiden)
            {
                Emit(evt.SequenceNumber, BroadcastCueCategory.CommentaryTrigger, BroadcastPriority.Normal, "Commentary.MaidenOver", evt.BowlerId);
            }
        }

        private void OnInningsCompleted(InningsCompletedEvent evt)
        {
            Emit(evt.SequenceNumber, BroadcastCueCategory.HudGraphic, BroadcastPriority.High, "Hud.InningsScorecard", $"{evt.TotalRuns}/{evt.WicketsLost}");
            Emit(evt.SequenceNumber, BroadcastCueCategory.CommentaryTrigger, BroadcastPriority.High, "Commentary.InningsBreak", evt.Reason.ToString());
        }

        private void OnMatchCompleted(MatchCompletedEvent evt)
        {
            Emit(evt.SequenceNumber, BroadcastCueCategory.CrowdReaction, BroadcastPriority.ImmediateInterrupt, "Crowd.MatchEndCelebration");
            Emit(evt.SequenceNumber, BroadcastCueCategory.HudGraphic, BroadcastPriority.ImmediateInterrupt, "Hud.MatchResult", evt.Result.Summary);
            Emit(evt.SequenceNumber, BroadcastCueCategory.CommentaryTrigger, BroadcastPriority.ImmediateInterrupt, "Commentary.MatchResult", evt.Result.Summary);
        }

        private void Emit(
            long sourceSequence,
            BroadcastCueCategory category,
            BroadcastPriority priority,
            string cueKey,
            string payload = "")
        {
            var cue = new BroadcastCue(sourceSequence, category, priority, cueKey, payload);
            _cueHistory.Add(cue);
            CueEmitted?.Invoke(cue);
        }
    }
}
