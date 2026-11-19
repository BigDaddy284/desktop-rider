#nullable enable

using System;
using NUnit.Framework;

namespace DesktopRider.Animation.Tests
{
    public class ModelAnimationDirectorTests
    {
        private static TimeSpan Seconds(double s) => TimeSpan.FromSeconds(s);

        /// <summary>Advances in small steps, as frames do.</summary>
        private static void Run(ModelAnimationDirector director, double seconds, bool moving = false)
        {
            for (double t = 0; t < seconds; t += 0.05)
            {
                director.Advance(Seconds(0.05), moving);
            }
        }

        [Test]
        public void TheClipList_MatchesTheModel_AndReadsAsAValidList()
        {
            var director = new ModelAnimationDirector();

            Assert.Equal(15, director.Manifest.Animations.Count);
            Assert.Equal("Idle", director.Manifest.Default.Name);
            Assert.True(director.Manifest["Sleeping"].Loop);
            Assert.Equal("SleepStart", director.Manifest["Sleeping"].Enter);
            Assert.Equal("SleepEnd", director.Manifest["Sleeping"].Exit);
            foreach (var choice in ModelAnimationDirector.IdleChoices)
            {
                Assert.True(director.Manifest.TryGet(choice, out _));
            }
        }

        [Test]
        public void StartsAtRest()
        {
            var director = new ModelAnimationDirector(new Random(1)) { IdleAnimations = false };

            director.Advance(Seconds(0.05), moving: false);

            Assert.Equal("Idle", director.Clip);
            Assert.Equal(1, director.Starts);
        }

        [Test]
        public void Moving_PlaysTheRidingLoop_AndStoppingGoesBackToRest()
        {
            var director = new ModelAnimationDirector(new Random(1)) { IdleAnimations = false };
            director.Advance(Seconds(0.05), moving: false);

            director.Advance(Seconds(0.05), moving: true);
            Assert.Equal(ModelAnimationDirector.StartClip, director.Clip);   // setting off: the start first
            Run(director, 30.0 / 24 + 0.1, moving: true);
            Assert.Equal(ModelAnimationDirector.RidingClip, director.Clip);
            int starts = director.Starts;

            Run(director, 3, moving: true);
            Assert.Equal(starts, director.Starts);      // the riding loop carries on, it is not restarted every frame

            director.Advance(Seconds(0.05), moving: false);
            Assert.Equal("Idle", director.Clip);
        }

        [Test]
        public void IdleMovements_PlayNowAndThen_WhenResting()
        {
            var director = new ModelAnimationDirector(new Random(3));
            bool sawOne = false;

            for (int i = 0; i < 30 * 20 && !sawOne; i++)
            {
                director.Advance(Seconds(0.05), moving: false);
                sawOne = Array.IndexOf(ModelAnimationDirector.IdleChoices, director.Clip) >= 0;
            }

            Assert.True(sawOne);
        }

        [Test]
        public void IdleMovements_NeverPlayWhileRiding()
        {
            var director = new ModelAnimationDirector(new Random(3));

            for (int i = 0; i < 30 * 20; i++)
            {
                director.Advance(Seconds(0.05), moving: true);
                Assert.Contains(director.Clip, new[] { ModelAnimationDirector.StartClip, ModelAnimationDirector.RidingClip });
            }
        }

        [Test]
        public void IdleMovements_CanBeSwitchedOff()
        {
            var director = new ModelAnimationDirector(new Random(3)) { IdleAnimations = false };

            Run(director, 40);

            Assert.Equal("Idle", director.Clip);
        }

        [Test]
        public void AOneShot_PlaysThenReturnsToRest()
        {
            var director = new ModelAnimationDirector(new Random(1)) { IdleAnimations = false };
            director.Advance(Seconds(0.05), moving: false);

            Assert.True(director.Perform("Wheelie"));
            director.Advance(Seconds(0.05), moving: false);
            Assert.Equal("Wheelie", director.Clip);

            Run(director, 60.0 / 24 + 0.5);
            Assert.Equal("Idle", director.Clip);
        }

        [Test]
        public void ATrick_ShowsEvenWhileMoving()
        {
            var director = new ModelAnimationDirector(new Random(1)) { IdleAnimations = false };
            director.Advance(Seconds(0.05), moving: true);

            director.Perform("Wheelie");
            director.Advance(Seconds(0.05), moving: true);

            Assert.Equal("Wheelie", director.Clip);
        }

        [Test]
        public void Sleeping_GoesThroughItsStartAndEnd()
        {
            var director = new ModelAnimationDirector(new Random(1)) { IdleAnimations = false };
            director.Advance(Seconds(0.05), moving: false);
            var seen = new System.Collections.Generic.List<string>();

            director.Perform("Sleeping");
            for (int i = 0; i < 400; i++)
            {
                director.Advance(Seconds(0.05), moving: false);
                if (seen.Count == 0 || seen[seen.Count - 1] != director.Clip) seen.Add(director.Clip);
            }

            Assert.Equal(new[] { "SleepStart", "Sleeping", "SleepEnd", "Idle" }, seen.ToArray());
        }

        [Test]
        public void TheSameOneShotTwice_StartsTwice()
        {
            var director = new ModelAnimationDirector(new Random(1)) { IdleAnimations = false };
            director.Advance(Seconds(0.05), moving: false);

            director.Perform("Wheelie");
            director.Perform("Wheelie");
            int startsBefore = director.Starts;
            Run(director, 2 * 60.0 / 24 + 1);

            // Wheelie, Wheelie again, then back to Idle: three fresh starts.
            Assert.InRange(director.Starts - startsBefore, 3, 4);
        }

        [Test]
        public void SettingOff_PlaysTheStartOnce_ThenRides()
        {
            var director = new ModelAnimationDirector(new Random(1)) { IdleAnimations = false };
            director.Advance(Seconds(0.05), moving: false);
            var seen = new System.Collections.Generic.List<string>();

            for (int i = 0; i < 100; i++)
            {
                director.Advance(Seconds(0.05), moving: true);
                if (seen.Count == 0 || seen[seen.Count - 1] != director.Clip) seen.Add(director.Clip);
            }

            Assert.Equal(new[] { "RideOff", "RidingLoop" }, seen.ToArray());
        }

        [Test]
        public void TheStart_TakesAsLongAsItsClip_AtTheChosenSpeed()
        {
            var director = new ModelAnimationDirector(new Random(1)) { IdleAnimations = false, Speed = 2 };
            director.Advance(Seconds(0.05), moving: true);

            Run(director, 30.0 / 24 / 2 - 0.15, moving: true);
            Assert.Equal("RideOff", director.Clip);
            Run(director, 0.25, moving: true);
            Assert.Equal("RidingLoop", director.Clip);
        }

        [Test]
        public void StoppingAndSettingOffAgain_PlaysTheStartAgain()
        {
            var director = new ModelAnimationDirector(new Random(1)) { IdleAnimations = false };
            Run(director, 3, moving: true);
            Run(director, 1, moving: false);

            director.Advance(Seconds(0.05), moving: true);

            Assert.Equal("RideOff", director.Clip);
        }

        [Test]
        public void AfterATrickWhileRolling_GoesStraightBackToRiding()
        {
            var director = new ModelAnimationDirector(new Random(1)) { IdleAnimations = false };
            Run(director, 3, moving: true);

            director.Perform("Wheelie");
            Run(director, 60.0 / 24 + 0.5, moving: true);

            Assert.Equal("RidingLoop", director.Clip);
        }

        [Test]
        public void UnknownClips_AreRefused()
        {
            Assert.False(new ModelAnimationDirector().Perform("Backflip on the moon"));
        }
    
        /// <summary>The sidecar crew's clips (Sidecar41_Desktop.fbx), as the app reads them from the model.</summary>
        private static readonly (string, int, bool)[] SidecarClips =
        {
            ("Idle", 48, true), ("LookAround", 72, false), ("Revving", 24, true), ("Thinking", 72, false), ("Talking", 48, true),
            ("Celebration", 60, false), ("SleepStart", 24, false), ("Sleeping", 96, true), ("SleepEnd", 24, false),
            ("RideOff", 42, false), ("RideOn", 48, false), ("RidingLoop", 32, true), ("CornerLeft", 32, true), ("CornerRight", 32, true),
        };

        [Test]
        public void AnotherModel_UsesItsOwnClips_AndItsOwnLookRound()
        {
            var director = new ModelAnimationDirector(SidecarClips, new Random(1));
            Assert.Equal(SidecarClips.Length, director.Manifest.Animations.Count);
            Assert.Equal(new[] { "LookAround" }, director.Idles);
            Assert.Equal("SleepStart", director.Manifest["Sleeping"].Enter);
            Assert.Equal(new[] { "IdleVariation", "LookLeft", "LookRight" }, new ModelAnimationDirector().Idles);

            var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 2400; i++) { director.Advance(Seconds(0.05), false); seen.Add(director.Clip); }
            Assert.True(seen.Contains("LookAround"), "the crew should look round now and then");
        }

        [Test]
        public void AnotherModel_SetsOffForAsLongAsItsOwnStartClip()
        {
            var director = new ModelAnimationDirector(SidecarClips, new Random(1)) { IdleAnimations = false };
            director.Advance(Seconds(0.05), moving: true);
            Run(director, 42.0 / 24 - 0.15, moving: true);
            Assert.Equal("RideOff", director.Clip);
            Run(director, 0.25, moving: true);
            Assert.Equal("RidingLoop", director.Clip);
        }

        [Test]
        public void AModelWithoutTheRidingClips_IsRefused()
        {
            Assert.Throws<ArgumentException>(() => new ModelAnimationDirector(new[] { ("Idle", 24, true), ("RideOff", 24, false) }));
        }
    }
}
