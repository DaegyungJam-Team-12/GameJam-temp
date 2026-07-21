#nullable enable

using System;
using Icebreaker.Shared.State;
using NUnit.Framework;

namespace Icebreaker.Core.Tests
{
    public sealed class GameLoopControllerTests
    {
        [Test]
        public void InitialPhase_IsTraveling()
        {
            var controller = new GameLoopController();

            Assert.That(controller.Phase, Is.EqualTo(GamePhase.Traveling));
        }

        [Test]
        public void VoyageTimerElapses_TransitionsToReady()
        {
            var controller = new GameLoopController();
            GamePhase? changedPhase = null;
            controller.PhaseChanged += phase => changedPhase = phase;

            controller.Tick(30d);

            Assert.That(controller.Phase, Is.EqualTo(GamePhase.Ready));
            Assert.That(changedPhase, Is.EqualTo(GamePhase.Ready));
        }

        [Test]
        public void RequestStageStart_FromReady_EntersCountdown()
        {
            var controller = new GameLoopController();
            controller.Tick(30d);

            controller.RequestStageStart();

            Assert.That(controller.Phase, Is.EqualTo(GamePhase.Countdown));
            Assert.That(controller.CountdownRemainingSeconds, Is.EqualTo(3d));
        }

        [Test]
        public void CountdownElapses_TransitionsToPlaying()
        {
            var controller = new GameLoopController();
            DriveToPlaying(controller);

            Assert.That(controller.Phase, Is.EqualTo(GamePhase.Playing));
            Assert.That(controller.StageElapsedSeconds, Is.EqualTo(0d));
        }

        [Test]
        public void StageClockAdvances_AndReachesStageEndingAt60()
        {
            var controller = new GameLoopController();
            DriveToPlaying(controller);

            controller.Tick(30d);

            Assert.That(controller.RemainingSeconds, Is.EqualTo(30d));
            Assert.That(controller.Phase, Is.EqualTo(GamePhase.Playing));

            controller.Tick(30d);

            Assert.That(controller.Phase, Is.EqualTo(GamePhase.StageEnding));
            Assert.That(controller.StageElapsedSeconds, Is.EqualTo(60d));
            Assert.That(controller.RemainingSeconds, Is.EqualTo(0d));
        }

        [Test]
        public void SettingsPause_FreezesStageClock()
        {
            var controller = new GameLoopController();
            DriveToPlaying(controller);

            controller.SetSettingsPaused(true);
            controller.Tick(10d);

            Assert.That(controller.StageElapsedSeconds, Is.EqualTo(0d));
            Assert.That(controller.Phase, Is.EqualTo(GamePhase.Playing));

            controller.SetSettingsPaused(false);
            controller.Tick(10d);

            Assert.That(controller.StageElapsedSeconds, Is.EqualTo(10d));
        }

        [Test]
        public void InvalidTransition_Throws()
        {
            var controller = new GameLoopController();

            Assert.Throws<InvalidOperationException>(() => controller.RequestStageStart());

            DriveToPlaying(controller);

            Assert.Throws<InvalidOperationException>(() => controller.EnterSettlement());
        }

        [Test]
        public void SettlementWithoutArrival_ReturnsToTraveling_AndResetsVoyage()
        {
            var controller = new GameLoopController();
            DriveToPlaying(controller);
            controller.Tick(60d);
            controller.EnterSettlement();

            controller.CompleteSettlement(false);

            Assert.That(controller.Phase, Is.EqualTo(GamePhase.Traveling));
            Assert.That(controller.VoyageRemainingSeconds, Is.EqualTo(30d));
        }

        [Test]
        public void SettlementWithArrival_ThenCompleteArrival_FinalDestination_Completed()
        {
            var controller = new GameLoopController();
            DriveToPlaying(controller);
            controller.Tick(60d);
            controller.EnterSettlement();

            controller.CompleteSettlement(true);

            Assert.That(controller.Phase, Is.EqualTo(GamePhase.Arrival));

            controller.CompleteArrival(true);

            Assert.That(controller.Phase, Is.EqualTo(GamePhase.Completed));
        }

        [Test]
        public void CompletedPhase_BlocksStageStart()
        {
            var controller = new GameLoopController();
            DriveToPlaying(controller);
            controller.Tick(60d);
            controller.EnterSettlement();
            controller.CompleteSettlement(true);
            controller.CompleteArrival(true);

            Assert.Throws<InvalidOperationException>(() => controller.RequestStageStart());
        }

        [Test]
        public void Constructor_RejectsNonPositiveDurations()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameLoopController(0d));
        }

        private static void DriveToPlaying(GameLoopController controller)
        {
            controller.Tick(30d);
            controller.RequestStageStart();
            controller.Tick(3d);
        }
    }
}
