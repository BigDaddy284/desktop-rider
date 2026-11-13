#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace DesktopRider.Animation.Tests
{
    public class SpriteManifestTests
    {
        [Test]
        public void Parse_ReadsFrameSizeAndAnimations()
        {
            var manifest = TestManifests.Create();

            Assert.Equal(100, manifest.FrameWidth);
            Assert.Equal(80, manifest.FrameHeight);
            Assert.Equal(10, manifest.FramesPerSecond);
            Assert.Equal(76, manifest.GroundY);
            Assert.Equal(8, manifest.Animations.Count);
            Assert.Equal("Idle", manifest.Default.Name);
            Assert.True(manifest["Idle"].Loop);
            Assert.False(manifest["Wave"].Loop);
        }

        [Test]
        public void Indexer_IgnoresCaseAndRejectsUnknownNames()
        {
            var manifest = TestManifests.Create();

            Assert.Same(manifest["Wave"], manifest["wave"]);
            Assert.True(manifest.TryGet("WHEELIE", out _));
            Assert.False(manifest.TryGet("Backflip", out _));
            Assert.Throws<KeyNotFoundException>(() => manifest["Backflip"]);
        }

        [Test]
        public void SheetFor_PicksTheSheetForTheDirection()
        {
            var clip = TestManifests.Create()["Wave"];

            Assert.Equal("Right/Wave.png", clip.SheetFor(Facing.Right));
            Assert.Equal("Left/Wave.png", clip.SheetFor(Facing.Left));
        }
        [TestCase(0, 0, 0)]
        [TestCase(2, 200, 0)]
        [TestCase(3, 0, 80)]
        [TestCase(4, 100, 80)]
        public void FrameRectFor_WalksTheGridRowByRow(int frame, int x, int y)
        {
            var manifest = TestManifests.Create();

            Assert.Equal(new FrameRect(x, y, 100, 80), manifest.FrameRectFor(manifest["Wave"], frame));
        }
        [TestCase(-1)]
        [TestCase(5)]
        public void FrameRectFor_RejectsFramesOutsideTheAnimation(int frame)
        {
            var manifest = TestManifests.Create();

            Assert.Throws<ArgumentOutOfRangeException>(() => manifest.FrameRectFor(manifest["Wave"], frame));
        }
        [TestCase("not json")]
        [TestCase("null")]
        [TestCase("{ \"frameWidth\": 0, \"frameHeight\": 80, \"framesPerSecond\": 10, \"animations\": [] }")]
        [TestCase("{ \"frameWidth\": 10, \"frameHeight\": 80, \"framesPerSecond\": 0, \"animations\": [ { \"name\": \"Idle\", \"loop\": true, \"frameCount\": 1, \"columns\": 1, \"right\": \"a\", \"left\": \"b\" } ] }")]
        [TestCase("{ \"frameWidth\": 10, \"frameHeight\": 80, \"framesPerSecond\": 10, \"animations\": [] }")]
        [TestCase("{ \"frameWidth\": 10, \"frameHeight\": 80, \"framesPerSecond\": 10, \"animations\": [ { \"name\": \"Idle\", \"loop\": true, \"frameCount\": 0, \"columns\": 1, \"right\": \"a\", \"left\": \"b\" } ] }")]
        [TestCase("{ \"frameWidth\": 10, \"frameHeight\": 80, \"framesPerSecond\": 10, \"animations\": [ { \"name\": \"Idle\", \"loop\": true, \"frameCount\": 1, \"columns\": 1, \"right\": \"\", \"left\": \"b\" } ] }")]
        [TestCase("{ \"frameWidth\": 10, \"frameHeight\": 80, \"framesPerSecond\": 10, \"animations\": [ { \"name\": \"Idle\", \"loop\": true, \"frameCount\": 1, \"columns\": 1, \"right\": \"a\", \"left\": \"b\" }, { \"name\": \"idle\", \"loop\": true, \"frameCount\": 1, \"columns\": 1, \"right\": \"a\", \"left\": \"b\" } ] }")]
        [TestCase("{ \"frameWidth\": 10, \"frameHeight\": 80, \"framesPerSecond\": 10, \"animations\": [ { \"name\": \"Wave\", \"loop\": false, \"frameCount\": 1, \"columns\": 1, \"right\": \"a\", \"left\": \"b\" } ] }")]
        [TestCase("{ \"frameWidth\": 10, \"frameHeight\": 80, \"framesPerSecond\": 10, \"animations\": [ { \"name\": \"Idle\", \"loop\": false, \"frameCount\": 1, \"columns\": 1, \"right\": \"a\", \"left\": \"b\" } ] }")]
        [TestCase("{ \"frameWidth\": 10, \"frameHeight\": 80, \"framesPerSecond\": 10, \"animations\": [ { \"name\": \"Idle\", \"loop\": true, \"frameCount\": 1, \"columns\": 1, \"right\": \"a\", \"left\": \"b\" }, { \"name\": \"Nap\", \"loop\": true, \"frameCount\": 1, \"columns\": 1, \"right\": \"a\", \"left\": \"b\", \"enter\": \"Missing\" } ] }")]
        [TestCase("{ \"frameWidth\": 10, \"frameHeight\": 80, \"framesPerSecond\": 10, \"animations\": [ { \"name\": \"Idle\", \"loop\": true, \"frameCount\": 1, \"columns\": 1, \"right\": \"a\", \"left\": \"b\" }, { \"name\": \"Nap\", \"loop\": true, \"frameCount\": 1, \"columns\": 1, \"right\": \"a\", \"left\": \"b\", \"exit\": \"Idle\" } ] }")]
        [TestCase("{ \"frameWidth\": 10, \"frameHeight\": 80, \"framesPerSecond\": 10, \"animations\": [ { \"name\": \"Idle\", \"loop\": true, \"frameCount\": 1, \"columns\": 1, \"right\": \"a\", \"left\": \"b\" }, { \"name\": \"Jump\", \"loop\": false, \"frameCount\": 1, \"columns\": 1, \"right\": \"a\", \"left\": \"b\", \"frameWidth\": 8, \"frameHeight\": 200 } ] }")]
        [TestCase("{ \"frameWidth\": 10, \"frameHeight\": 80, \"framesPerSecond\": 10, \"animations\": [ { \"name\": \"Idle\", \"loop\": true, \"frameCount\": 1, \"columns\": 1, \"right\": \"a\", \"left\": \"b\" }, { \"name\": \"Jump\", \"loop\": false, \"frameCount\": 1, \"columns\": 1, \"right\": \"a\", \"left\": \"b\", \"frameWidth\": 30, \"frameHeight\": 200, \"originX\": 25 } ] }")]
        [TestCase("{ \"frameWidth\": 10, \"frameHeight\": 80, \"framesPerSecond\": 10, \"animations\": [ { \"name\": \"Idle\", \"loop\": true, \"frameCount\": 1, \"columns\": 1, \"right\": \"a\", \"left\": \"b\" }, { \"name\": \"Jump\", \"loop\": false, \"frameCount\": 1, \"columns\": 1, \"right\": \"a\", \"left\": \"b\", \"frameWidth\": 30, \"frameHeight\": 200, \"originY\": -1 } ] }")]
        [TestCase("{ \"frameWidth\": 10, \"frameHeight\": 80, \"framesPerSecond\": 10, \"animations\": [ { \"name\": \"Idle\", \"loop\": true, \"frameCount\": 1, \"columns\": 1, \"right\": \"a\", \"left\": \"b\", \"originX\": 2 } ] }")]
        public void Parse_ExplainsWhatIsWrongWithABadManifest(string json)
        {
            var error = Assert.Throws<InvalidDataException>(() => SpriteManifest.Parse(json));

            Assert.False(string.IsNullOrWhiteSpace(error.Message));
        }

        [Test]
        public void RealRiderManifest_IsValidAndCoversEveryRequiredAnimation()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "RiderSprites", "sprites.json");
            if (!File.Exists(path))
            {
                return; // The sprites are not part of this checkout.
            }

            var manifest = SpriteManifest.Parse(File.ReadAllText(path));

            string[] required =
            new[]
            {
                "Idle", "IdleVariation", "LookLeft", "LookRight", "Revving", "Thinking", "Talking",
                "Wheelie", "Celebration", "Sleeping", "RideOff", "RideOn", "RidingLoop",
            };
            Assert.All(required, name => Assert.True(manifest.TryGet(name, out _), $"{name} is missing"));
            Assert.Equal("SleepStart", manifest["Sleeping"].Enter);
            Assert.Equal("SleepEnd", manifest["Sleeping"].Exit);
            Assert.Equal("RideOff", manifest["RidingLoop"].Enter);
            Assert.Equal("RideOn", manifest["RidingLoop"].Exit);
        }

        [Test]
        public void OrdinaryAnimations_UseTheStandardFrameAndNeedNoStage()
        {
            var manifest = TestManifests.Create();
            var wave = manifest["Wave"];

            Assert.False(wave.HasOwnFrame);
            Assert.Equal((100, 80), manifest.FrameSizeFor(wave));
            Assert.Equal((0, 0), manifest.OriginFor(wave, Facing.Right));
            Assert.Equal((0, 0), manifest.OriginFor(wave, Facing.Left));
        }

        [Test]
        public void AJump_HasItsOwnLargerFrame()
        {
            var manifest = TestManifests.Create();
            var jump = manifest["Jump"];

            Assert.True(jump.HasOwnFrame);
            Assert.Equal((180, 200), manifest.FrameSizeFor(jump));
            Assert.Equal(new FrameRect(0, 0, 180, 200), manifest.FrameRectFor(jump, 0));
            Assert.Equal(new FrameRect(540, 0, 180, 200), manifest.FrameRectFor(jump, 3));
            Assert.Equal(new FrameRect(180, 200, 180, 200), manifest.FrameRectFor(jump, 5));
        }

        [Test]
        public void AJump_KeepsTheCharacterInPlaceWhicheverWayItFaces()
        {
            var manifest = TestManifests.Create();
            var jump = manifest["Jump"];

            // Facing right the standard frame starts 30 pixels in. The left-facing sheet is a mirror image,
            // so the 50 pixels of room that were on the right are now on the left.
            Assert.Equal((30, 120), manifest.OriginFor(jump, Facing.Right));
            Assert.Equal((50, 120), manifest.OriginFor(jump, Facing.Left));
        }

        [Test]
        public void RealRiderManifest_JumpsAndTricksStartFromTheRidersUsualPlace()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "RiderSprites", "sprites.json");
            if (!File.Exists(path))
            {
                return; // The sprites are not part of this checkout.
            }

            var manifest = SpriteManifest.Parse(File.ReadAllText(path));
            var big = manifest.Animations.Where(clip => clip.HasOwnFrame).ToList();

            Assert.True(big.Count >= 10, "At least ten jump, trick and whoop animations are expected.");
            Assert.All(big, clip =>
            {
                // One-shots that begin and end at rest, with room above for the jump and room in front for a ramp to arrive.
                Assert.False(clip.Loop);
                var (width, height) = manifest.FrameSizeFor(clip);
                var (x, y) = manifest.OriginFor(clip, Facing.Right);
                Assert.True(width > manifest.FrameWidth && height > manifest.FrameHeight, clip.Name);
                Assert.Equal(height - manifest.FrameHeight, y);
                Assert.True(width - manifest.FrameWidth - x > 0, clip.Name);

                // At the normal on-screen size (45%) the larger frame must sit on a whole pixel, or the rider would shift.
                Assert.Equal(0, x % 20);
                Assert.Equal(0, y % 20);
            });
        }
    }
}
