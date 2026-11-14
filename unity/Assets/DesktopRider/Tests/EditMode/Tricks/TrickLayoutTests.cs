#nullable enable

using System;
using System.Linq;
using System.Numerics;
using DesktopRider.Tricks;
using NUnit.Framework;

namespace DesktopRider.Tricks.Tests
{
    public class TrickLayoutTests
    {
        // The same shape as the file build_unity_props.py writes, cut down to two tricks.
        internal const string Sample = @"{
 ""units"": ""metres"", ""fps"": 24, ""note"": ""Props are static."",
 ""props"": {
  ""DR_Prop_JumpRamp"": {""file"": ""DR_Prop_JumpRamp.fbx"", ""kind"": ""dirt"", ""length"": 1.5, ""width"": 1.1, ""height"": 0.35, ""triangles"": 400, ""materials"": [""DR_Dirt""]},
  ""DR_Prop_JumpLanding"": {""file"": ""DR_Prop_JumpLanding.fbx"", ""kind"": ""dirt"", ""length"": 3.0, ""width"": 1.1, ""height"": 0.45, ""triangles"": 500, ""materials"": [""DR_Dirt""]},
  ""DR_Prop_Whoops"": {""file"": ""DR_Prop_Whoops.fbx"", ""kind"": ""dirt"", ""length"": 9.1, ""width"": 1.1, ""height"": 0.3, ""triangles"": 900, ""materials"": [""DR_Dirt""]}
 },
 ""animations"": [
  {""name"": ""Jump"", ""stage"": ""air"", ""start"": 1, ""end"": 5, ""frames"": 4,
   ""props"": [{""prop"": ""DR_Prop_JumpRamp"", ""x"": 1.2, ""length"": 1.5}, {""prop"": ""DR_Prop_JumpLanding"", ""x"": 4.0, ""length"": 3.0}],
   ""travel"": [0.0, 0.5, 1.5, 3.0, 6.0], ""info"": {""T"": 1.15}},
  {""name"": ""Whoops"", ""stage"": ""ground"", ""start"": 10, ""end"": 12, ""frames"": 2,
   ""props"": [{""prop"": ""DR_Prop_Whoops"", ""x"": 1.0, ""length"": 9.1}],
   ""travel"": [0.0, 1.0, 2.0], ""info"": {}}
 ]
}";

        [Test]
        public void ReadsPropsAndTricks()
        {
            var layout = TrickLayout.Parse(Sample);

            Assert.Equal(24.0, layout.FramesPerSecond, 6);
            Assert.Equal(3, layout.Props.Count);
            Assert.Equal(new[] { "Jump", "Whoops" }, layout.TrickNames.OrderBy(n => n).ToArray());
            var ramp = layout.Prop("DR_Prop_JumpRamp");
            Assert.NotNull(ramp);
            Assert.Equal("dirt", ramp.Kind);
            Assert.Equal(0.35, ramp.Height, 6);

            var jump = layout.Find("Jump");
            Assert.NotNull(jump);
            Assert.Equal("air", jump.Stage);
            Assert.Equal(4, jump.Frames);
            Assert.Equal(2, jump.Props.Count);
            Assert.Equal(4.0, jump.Props[1].X, 6);
            Assert.Equal(6.0, jump.Distance, 6);
            Assert.Equal(4 / 24.0, jump.Seconds, 9);
            Assert.Equal(4 / 24.0, jump.Duration.TotalSeconds, 6);
            Assert.Null(layout.Find("Backflip"));
        }

        [Test]
        public void TravelIsSmoothBetweenFrames_AndHeldAtTheEnds()
        {
            var jump = TrickLayout.Parse(Sample).Find("Jump")!;

            Assert.Equal(0.0, jump.TravelAt(-1), 6);
            Assert.Equal(0.5, jump.TravelAt(1 / 24.0), 6);
            Assert.Equal(1.0, jump.TravelAt(1.5 / 24.0), 6);
            Assert.Equal(6.0, jump.TravelAt(10), 6);
        }

        [Test]
        public void ReachCoversTheBikeAndEveryProp()
        {
            var layout = TrickLayout.Parse(Sample);

            var (from, to) = layout.Find("Jump")!.Reach;
            Assert.Equal(0.0, from, 6);
            Assert.Equal(7.0, to, 6);            // the landing ends at 4 + 3, past where the bike stops (6)
            Assert.Equal(10.1, layout.Find("Whoops")!.Reach.To, 6);
        }

        [Test]
        public void ExplainsWhatIsWrongWithABrokenFile()
        {
            Assert.Contains("not valid JSON", Assert.Throws<FormatException>(() => TrickLayout.Parse("{ nope")).Message);
            Assert.Contains("metres", Assert.Throws<FormatException>(() => TrickLayout.Parse(@"{""units"":""feet"",""fps"":24}")).Message);
            Assert.Contains("fps", Assert.Throws<FormatException>(() => TrickLayout.Parse(@"{""units"":""metres""}")).Message);

            string wrongCount = Sample.Replace(@"""travel"": [0.0, 1.0, 2.0]", @"""travel"": [0.0, 1.0]");
            Assert.Contains("one more than the frames", Assert.Throws<FormatException>(() => TrickLayout.Parse(wrongCount)).Message);

            string unknownProp = Sample.Replace(@"{""prop"": ""DR_Prop_Whoops""", @"{""prop"": ""DR_Prop_Missing""");
            Assert.Contains("DR_Prop_Missing", Assert.Throws<FormatException>(() => TrickLayout.Parse(unknownProp)).Message);
        }

        [Test]
        public void AnEmptyLayoutHasNoTricks()
        {
            var layout = TrickLayout.Parse(@"{""units"":""metres"",""fps"":24}");
            Assert.Empty(layout.Animations);
            Assert.Empty(layout.Props);
        }
    }

    public class TrickRunTests
    {
        private static TrickAnimation Jump => TrickLayout.Parse(TrickLayoutTests.Sample).Find("Jump")!;

        [Test]
        public void PutsThePropsAheadOfTheBike_InTheWayItFaces()
        {
            var run = new TrickRun(Jump, new Vector2(10, 20), 90f);    // facing +X

            Assert.Equal(2, run.Props.Count);
            Assert.Equal("DR_Prop_JumpRamp", run.Props[0].Name);
            Assert.Equal(11.2, run.Props[0].Position.X, 4);
            Assert.Equal(20.0, run.Props[0].Position.Y, 4);
            Assert.Equal(14.0, run.Props[1].Position.X, 4);
            Assert.Equal(90.0, run.Props[1].Heading, 4);
        }

        [Test]
        public void MovesTheBikeAlongAndFinishes()
        {
            var run = new TrickRun(Jump, Vector2.Zero, 0f);     // facing +Y
            Assert.False(run.IsFinished);
            Assert.Equal(0.0, run.Position.Y, 6);

            run.Advance(2 / 24.0);
            Assert.Equal(1.5, run.Position.Y, 4);
            Assert.Equal(0.0, run.Position.X, 4);
            Assert.Equal(0.5, run.Progress, 4);

            run.Advance(TimeSpan.FromSeconds(1));      // the TimeSpan form works too
            Assert.True(run.IsFinished);
            Assert.Equal(6.0, run.Position.Y, 4);
            Assert.Equal(run.End.Y, run.Position.Y, 4);
            Assert.Equal(1.0, run.Progress, 6);
        }

        [Test]
        public void PlaysFasterOrSlowerWithTheAnimationSpeed()
        {
            var run = new TrickRun(Jump, Vector2.Zero, 0f) { Speed = 2.0 };
            run.Advance(1 / 24.0);
            Assert.Equal(1.5, run.Position.Y, 4);
            run.Advance(TimeSpan.FromSeconds(-1));
            run.Advance(double.NaN);
            Assert.Equal(1.5, run.Position.Y, 4);
        }

        [Test]
        public void KeepsTheWayItFaces_WhenThereIsRoom()
        {
            var heading = TrickRun.PickHeading(Jump, Vector2.Zero, 45f, p => true);
            Assert.Equal(45f, heading);
        }

        [Test]
        public void TurnsAsLittleAsPossible_ToKeepTheTrickOnScreen()
        {
            // Room only to the right (X from -1 to 20) and a little up and down.
            Func<Vector2, bool> fits = p => p.X > -1 && p.X < 20 && Math.Abs(p.Y) < 3;

            var heading = TrickRun.PickHeading(Jump, Vector2.Zero, 0f, fits);

            Assert.Equal(90f, heading);        // facing up does not fit; turned 30 or 60 degrees it still leaves the screen at the top
        }

        [Test]
        public void TurnsRoundWhenFacingAWall()
        {
            Func<Vector2, bool> fits = p => p.Y < 1 && p.Y > -20 && Math.Abs(p.X) < 1;

            Assert.Equal(180f, TrickRun.PickHeading(Jump, Vector2.Zero, 0f, fits));
        }

        [Test]
        public void GivesUpWhenNothingFits()
        {
            Assert.Null(TrickRun.PickHeading(Jump, Vector2.Zero, 0f, p => p.Length() < 2));
        }

        [Test]
        public void HeadingsWrapRound()
        {
            Assert.Equal(350f, TrickRun.Normalise(-10f));
            Assert.Equal(10f, TrickRun.Normalise(370f));
            Assert.Equal(0.0, TrickRun.Direction(90f).Y, 5);
            Assert.Equal(1.0, TrickRun.Direction(90f).X, 5);
        }
    }

    public class TrickPickerTests
    {
        private static readonly TrickLayout Layout = TrickLayout.Parse(TrickLayoutTests.Sample);

        [Test]
        public void NeverTheSameTrickTwiceInARow()
        {
            var picker = new TrickPicker(new Random(1));
            string? last = null;
            for (int i = 0; i < 20; i++)
            {
                var next = picker.Next(Layout.Animations);
                Assert.NotNull(next);
                Assert.NotEqual(last, next);
                last = next;
            }
        }

        [Test]
        public void OnlyGroundTricksWhenAsked()
        {
            var picker = new TrickPicker(new Random(2));
            for (int i = 0; i < 5; i++) Assert.Equal("Whoops", picker.Next(Layout.Animations, groundOnly: true));
        }

        [Test]
        public void NothingWhenThereAreNoTricks()
        {
            Assert.Null(new TrickPicker().Next(Array.Empty<TrickAnimation>()));
        }
    }
}
