#nullable enable

using System;
using NUnit.Framework;
using static DesktopRider.Animation.Tests.TestManifests;

namespace DesktopRider.Animation.Tests
{
    public class IdleVariationPickerTests
    {
        private static (AnimationPlayer Player, IdleVariationPicker Picker) Create(params string[] choices)
        {
            var player = new AnimationPlayer(TestManifests.Create());
            var picker = new IdleVariationPicker(player, choices, new Random(1))
            {
                MinGap = TimeSpan.FromSeconds(5),
                MaxGap = TimeSpan.FromSeconds(5),
            };
            return (player, picker);
        }

        [Test]
        public void DoesNothingBeforeTheGapHasPassed()
        {
            var (player, picker) = Create("Wave");

            picker.Advance(TimeSpan.FromSeconds(4));

            Assert.True(player.IsResting);
        }

        [Test]
        public void AsksForAnIdleAnimationOnceTheGapHasPassed()
        {
            var (player, picker) = Create("Wave");
            picker.MinGap = picker.MaxGap = TimeSpan.FromSeconds(5);

            picker.Advance(TimeSpan.FromSeconds(30));

            Assert.Equal("Wave", player.Current.Name);
        }

        [Test]
        public void DoesNothingWhenSwitchedOff()
        {
            var (player, picker) = Create("Wave");
            picker.Enabled = false;

            picker.Advance(TimeSpan.FromMinutes(10));

            Assert.True(player.IsResting);
        }

        [Test]
        public void LeavesTheRiderAloneWhileSomethingElseIsPlaying()
        {
            var (player, picker) = Create("Wave");
            player.Request("Talking");

            picker.Advance(TimeSpan.FromMinutes(10));

            Assert.Equal("Talking", player.Current.Name);
        }

        [Test]
        public void CopesWithNoChoices()
        {
            var (player, picker) = Create();

            picker.Advance(TimeSpan.FromMinutes(10));

            Assert.True(player.IsResting);
        }

        [Test]
        public void WaitsAgainAfterEachIdleAnimation()
        {
            var (player, picker) = Create("Wave");
            picker.Advance(TimeSpan.FromSeconds(30));
            player.Advance(Frames(6));
            Assert.True(player.IsResting);

            picker.Advance(TimeSpan.FromSeconds(1));

            Assert.True(player.IsResting);
        }
    }
}
