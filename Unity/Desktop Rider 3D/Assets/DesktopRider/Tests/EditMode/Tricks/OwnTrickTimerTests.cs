#nullable enable

using System;
using DesktopRider.Core.Configuration;
using NUnit.Framework;

namespace DesktopRider.Tricks.Tests
{
    public class OwnTrickTimerTests
    {
        [Test]
        public void Never_is_never_due()
        {
            var timer = new OwnTrickTimer(new Random(1));
            for (double t = 0; t < 100000; t += 30)
            {
                Assert.False(timer.Due(t, TrickFrequency.Never, true));
            }
            Assert.Equal(double.PositiveInfinity, timer.NextAt);
        }

        [TestCase(TrickFrequency.Sometimes, 300.0, 600.0)]
        [TestCase(TrickFrequency.Often, 120.0, 240.0)]
        public void The_gap_between_tricks_stays_in_its_range(TrickFrequency frequency, double min, double max)
        {
            var timer = new OwnTrickTimer(new Random(7));
            timer.Due(0, frequency, true);
            double last = 0; int count = 0;
            for (double t = 0; t < 20000; t += 1)
            {
                if (!timer.Due(t, frequency, true)) continue;
                Assert.InRange(t - last, min, max + 1);
                last = t; count++;
            }
            Assert.True(count > 20000 / (max + 1) - 2);
        }

        [Test]
        public void The_first_trick_waits_a_full_gap()
        {
            var timer = new OwnTrickTimer(new Random(3));
            Assert.False(timer.Due(1000, TrickFrequency.Often, true));
            Assert.InRange(timer.NextAt, 1120.0, 1240.0);
        }

        [Test]
        public void A_due_trick_that_cannot_start_is_tried_again_later()
        {
            var timer = new OwnTrickTimer(new Random(5));
            timer.Due(0, TrickFrequency.Often, true);
            var due = timer.NextAt;
            Assert.False(timer.Due(due, TrickFrequency.Often, false));
            Assert.Equal(due + OwnTrickTimer.RetrySeconds, timer.NextAt);
            Assert.False(timer.Due(due + 5, TrickFrequency.Often, true));
            Assert.True(timer.Due(due + OwnTrickTimer.RetrySeconds, TrickFrequency.Often, true));
        }

        [Test]
        public void A_trick_that_did_not_start_after_all_is_tried_again_soon()
        {
            var timer = new OwnTrickTimer(new Random(13));
            timer.Due(0, TrickFrequency.Sometimes, true);
            var due = timer.NextAt;
            Assert.True(timer.Due(due, TrickFrequency.Sometimes, true));
            timer.Retry(due);
            Assert.Equal(due + OwnTrickTimer.RetrySeconds, timer.NextAt);
            timer.Due(due + 30, TrickFrequency.Never, true);
            timer.Retry(due + 30);
            Assert.Equal(double.PositiveInfinity, timer.NextAt);
        }

        [Test]
        public void Changing_the_setting_starts_a_fresh_wait()
        {
            var timer = new OwnTrickTimer(new Random(9));
            timer.Due(0, TrickFrequency.Sometimes, true);
            Assert.False(timer.Due(100, TrickFrequency.Often, true));
            Assert.InRange(timer.NextAt, 220.0, 340.0);
            Assert.False(timer.Due(200, TrickFrequency.Never, true));
            Assert.Equal(double.PositiveInfinity, timer.NextAt);
        }

        [Test]
        public void Restart_waits_a_full_gap_again()
        {
            var timer = new OwnTrickTimer(new Random(11));
            timer.Due(0, TrickFrequency.Often, true);
            timer.Restart(500);
            Assert.InRange(timer.NextAt, 620.0, 740.0);
        }

        [Test]
        public void A_settings_file_from_before_the_setting_existed_reads_as_sometimes()
        {
            var json = "{\"schemaVersion\":1,\"behaviour\":{\"freeRiding\":true,\"animationFrequency\":\"normal\"}}";
            var settings = DesktopRider.Core.Infrastructure.JsonText.Read<AppSettings>(json, DesktopRider.Core.Infrastructure.JsonText.LocalFileSettings);
            Assert.NotNull(settings);
            Assert.True(settings!.Behaviour.FreeRiding);
            Assert.Equal(TrickFrequency.Sometimes, SettingsNormalizer.Normalize(settings).Behaviour.TricksByHimself);
            var back = DesktopRider.Core.Infrastructure.JsonText.Write(settings, DesktopRider.Core.Infrastructure.JsonText.LocalFileSettings);
            Assert.Contains("\"tricksByHimself\": \"sometimes\"", back);
        }

        [Test]
        public void New_settings_start_at_sometimes_and_a_bad_value_is_put_right()
        {
            Assert.Equal(TrickFrequency.Sometimes, new AppSettings().Behaviour.TricksByHimself);
            var settings = new AppSettings();
            settings.Behaviour.TricksByHimself = (TrickFrequency)42;
            SettingsNormalizer.Normalize(settings);
            Assert.Equal(TrickFrequency.Sometimes, settings.Behaviour.TricksByHimself);
        }
    }
}
