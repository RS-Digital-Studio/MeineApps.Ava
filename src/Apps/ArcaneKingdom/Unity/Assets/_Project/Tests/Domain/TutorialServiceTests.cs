#nullable enable
using System.Collections.Generic;
using System.Reflection;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Domain.Tutorial;
using ArcaneKingdom.Game.Tutorial;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class TutorialServiceTests
    {
        private sealed class StubAnalytics : IAnalyticsService
        {
            public List<string> Events { get; } = new();
            public void Track(string eventName, IReadOnlyDictionary<string, object>? properties = null) => Events.Add(eventName);
            public void SetUserProperty(string key, string value) { }
            public void SetUserId(string userId) { }
        }

        private static TutorialService BuildSubject(out StubAnalytics analytics, params TutorialStep[] steps)
        {
            analytics = new StubAnalytics();
            var svc = new TutorialService(analytics);
            // Steps via Reflection injizieren (statt JSON-Load)
            var field = typeof(TutorialService).GetField("_steps", BindingFlags.Instance | BindingFlags.NonPublic);
            var list = (List<TutorialStep>)field!.GetValue(svc)!;
            list.Clear();
            list.AddRange(steps);
            return svc;
        }

        [Test]
        public void OnEventFeuertPassendenSchritt()
        {
            var svc = BuildSubject(out var analytics,
                new TutorialStep { Id = "welcome", Order = 1, TriggerEvent = "first_session_start" });
            TutorialStep? requested = null;
            svc.StepRequested += s => requested = s;
            var fired = svc.OnEvent("first_session_start");
            Assert.IsTrue(fired);
            Assert.IsNotNull(requested);
            Assert.AreEqual("welcome", requested!.Id);
            CollectionAssert.Contains(analytics.Events, "tutorial_step_shown");
        }

        [Test]
        public void OnEventGibtFalseFuerUnbekanntenTrigger()
        {
            var svc = BuildSubject(out _, new TutorialStep { Id = "a", Order = 1, TriggerEvent = "x" });
            Assert.IsFalse(svc.OnEvent("y"));
        }

        [Test]
        public void MarkStepCompletedVerhindertWiederholung()
        {
            var svc = BuildSubject(out _, new TutorialStep { Id = "hub", Order = 1, TriggerEvent = "hub_entered" });
            var count = 0;
            svc.StepRequested += _ => count++;
            svc.OnEvent("hub_entered");
            svc.MarkStepCompleted("hub");
            svc.OnEvent("hub_entered");
            Assert.AreEqual(1, count, "Markierter Schritt wird nicht erneut gefeuert.");
        }

        [Test]
        public void SkipBeendetSofort()
        {
            var svc = BuildSubject(out var analytics,
                new TutorialStep { Id = "a", Order = 1, TriggerEvent = "t1" },
                new TutorialStep { Id = "b", Order = 2, TriggerEvent = "t2" });
            var finished = false;
            svc.TutorialFinished += () => finished = true;
            svc.Skip();
            Assert.IsTrue(svc.IsSkipped);
            Assert.IsTrue(finished);
            Assert.IsFalse(svc.OnEvent("t1"), "Nach Skip kein Step mehr.");
            CollectionAssert.Contains(analytics.Events, "tutorial_skipped");
        }

        [Test]
        public void RestoreProgressAusgewaehlterStepUebersprungen()
        {
            var svc = BuildSubject(out _,
                new TutorialStep { Id = "a", Order = 1, TriggerEvent = "t1" },
                new TutorialStep { Id = "b", Order = 2, TriggerEvent = "t2" },
                new TutorialStep { Id = "c", Order = 3, TriggerEvent = "t3" });
            svc.RestoreProgress(new TutorialProgress { CurrentStepId = "c" });
            Assert.IsFalse(svc.OnEvent("t1"), "Schritt a ist als completed importiert.");
            Assert.IsFalse(svc.OnEvent("t2"), "Schritt b ist als completed importiert.");
            Assert.IsTrue(svc.OnEvent("t3"));
        }

        [Test]
        public void FinishedNachAllenSchritten()
        {
            var svc = BuildSubject(out _,
                new TutorialStep { Id = "a", Order = 1, TriggerEvent = "t1" },
                new TutorialStep { Id = "b", Order = 2, TriggerEvent = "t2" });
            var finished = false;
            svc.TutorialFinished += () => finished = true;
            svc.MarkStepCompleted("a");
            svc.MarkStepCompleted("b");
            Assert.IsTrue(finished);
            Assert.IsTrue(svc.IsFinished);
        }
    }
}
