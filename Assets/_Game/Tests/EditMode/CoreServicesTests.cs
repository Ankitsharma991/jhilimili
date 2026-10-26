using System;
using System.Collections.Generic;
using NUnit.Framework;
using UtsavNights.Core.Services;

namespace UtsavNights.Tests
{
    public class ServiceRegistryTests
    {
        private interface IFoo
        {
        }

        private sealed class Foo : IFoo
        {
        }

        [Test]
        public void RegisterAndGet_ByInterface()
        {
            var registry = new ServiceRegistry();
            var foo = new Foo();
            registry.Register<IFoo>(foo);
            Assert.AreSame(foo, registry.Get<IFoo>());
            Assert.IsTrue(registry.TryGet(out IFoo found));
            Assert.AreSame(foo, found);
            Assert.IsTrue(registry.IsRegistered<IFoo>());
        }

        [Test]
        public void MissingService_ThrowsHelpfully_AndTryGetIsFalse()
        {
            var registry = new ServiceRegistry();
            var e = Assert.Throws<InvalidOperationException>(() => registry.Get<IFoo>());
            StringAssert.Contains("IFoo", e.Message);
            Assert.IsFalse(registry.TryGet(out IFoo _));
        }

        [Test]
        public void DoubleRegister_Throws_AndUnregisterAllowsReplacement()
        {
            var registry = new ServiceRegistry();
            registry.Register<IFoo>(new Foo());
            Assert.Throws<InvalidOperationException>(() => registry.Register<IFoo>(new Foo()));
            Assert.IsTrue(registry.Unregister<IFoo>());
            registry.Register<IFoo>(new Foo());
            Assert.AreEqual(1, registry.Count);
        }

        [Test]
        public void NullService_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ServiceRegistry().Register<IFoo>(null));
        }
    }

    public class EventBusTests
    {
        private struct Ping
        {
            public int Value;
        }

        private struct Other
        {
        }

        [Test]
        public void Publish_ReachesSubscribersOfThatTypeOnly()
        {
            var bus = new EventBus();
            int got = 0;
            int other = 0;
            bus.Subscribe<Ping>(p => got += p.Value);
            bus.Subscribe<Other>(_ => other++);
            bus.Publish(new Ping { Value = 5 });
            Assert.AreEqual(5, got);
            Assert.AreEqual(0, other);
        }

        [Test]
        public void PublishWithoutSubscribers_IsFine()
        {
            Assert.DoesNotThrow(() => new EventBus().Publish(new Ping()));
        }

        [Test]
        public void DuplicateSubscribe_DeliversOnce()
        {
            var bus = new EventBus();
            int got = 0;
            Action<Ping> handler = _ => got++;
            bus.Subscribe(handler);
            bus.Subscribe(handler);
            bus.Publish(new Ping());
            Assert.AreEqual(1, got);
            Assert.AreEqual(1, bus.SubscriberCount<Ping>());
        }

        [Test]
        public void Unsubscribe_StopsDelivery()
        {
            var bus = new EventBus();
            int got = 0;
            Action<Ping> handler = _ => got++;
            bus.Subscribe(handler);
            bus.Unsubscribe(handler);
            bus.Publish(new Ping());
            Assert.AreEqual(0, got);
            Assert.AreEqual(0, bus.SubscriberCount<Ping>());
        }

        [Test]
        public void UnsubscribeDuringPublish_IsSafe_AndSkipsLaterRemovedHandler()
        {
            var bus = new EventBus();
            var log = new List<string>();
            Action<Ping> second = _ => log.Add("second");
            Action<Ping> first = null;
            first = _ =>
            {
                log.Add("first");
                bus.Unsubscribe(first);
                bus.Unsubscribe(second);
            };
            bus.Subscribe(first);
            bus.Subscribe(second);
            bus.Publish(new Ping());
            bus.Publish(new Ping());
            CollectionAssert.AreEqual(new[] { "first" }, log);
            Assert.AreEqual(0, bus.SubscriberCount<Ping>());
        }

        [Test]
        public void SubscribeDuringPublish_TakesEffectNextPublish()
        {
            var bus = new EventBus();
            int late = 0;
            bool added = false;
            bus.Subscribe<Ping>(_ =>
            {
                if (!added)
                {
                    added = true;
                    bus.Subscribe<Ping>(__ => late++);
                }
            });
            bus.Publish(new Ping());
            Assert.AreEqual(0, late);
            bus.Publish(new Ping());
            Assert.AreEqual(1, late);
        }

        [Test]
        public void ThrowingHandler_IsReported_AndOthersStillRun()
        {
            var bus = new EventBus();
            Exception reported = null;
            int ran = 0;
            bus.HandlerFailed += (type, e) => reported = e;
            bus.Subscribe<Ping>(_ => throw new InvalidOperationException("boom"));
            bus.Subscribe<Ping>(_ => ran++);
            bus.Publish(new Ping());
            Assert.AreEqual(1, ran);
            Assert.IsInstanceOf<InvalidOperationException>(reported);
        }
    }

    public class TickLoopTests
    {
        private sealed class Counter : ITickable
        {
            public float Total;
            public int Calls;
            public Action OnTick;

            public void Tick(float dt)
            {
                Total += dt;
                Calls++;
                OnTick?.Invoke();
            }
        }

        [Test]
        public void TicksEveryItem()
        {
            var loop = new TickLoop();
            var a = new Counter();
            var b = new Counter();
            loop.Add(a);
            loop.Add(b);
            loop.Add(a);
            loop.Tick(0.5f);
            Assert.AreEqual(0.5f, a.Total);
            Assert.AreEqual(1, a.Calls, "duplicate add is ignored");
            Assert.AreEqual(0.5f, b.Total);
            Assert.AreEqual(2, loop.Count);
        }

        [Test]
        public void Pause_StopsTicks()
        {
            var loop = new TickLoop();
            var a = new Counter();
            loop.Add(a);
            loop.IsPaused = true;
            loop.Tick(1f);
            Assert.AreEqual(0, a.Calls);
            loop.IsPaused = false;
            loop.Tick(1f);
            Assert.AreEqual(1, a.Calls);
        }

        [Test]
        public void RemoveSelfDuringTick_IsSafe()
        {
            var loop = new TickLoop();
            var a = new Counter();
            var b = new Counter();
            a.OnTick = () => loop.Remove(a);
            loop.Add(a);
            loop.Add(b);
            loop.Tick(1f);
            loop.Tick(1f);
            Assert.AreEqual(1, a.Calls);
            Assert.AreEqual(2, b.Calls);
            Assert.IsFalse(loop.Contains(a));
        }

        [Test]
        public void RemoveLaterItemDuringTick_SkipsItThisFrame()
        {
            var loop = new TickLoop();
            var a = new Counter();
            var b = new Counter();
            a.OnTick = () => loop.Remove(b);
            loop.Add(a);
            loop.Add(b);
            loop.Tick(1f);
            Assert.AreEqual(0, b.Calls);
        }

        [Test]
        public void AddDuringTick_StartsNextFrame()
        {
            var loop = new TickLoop();
            var a = new Counter();
            var b = new Counter();
            a.OnTick = () => loop.Add(b);
            loop.Add(a);
            loop.Tick(1f);
            Assert.AreEqual(0, b.Calls);
            Assert.IsTrue(loop.Contains(b));
            loop.Tick(1f);
            Assert.AreEqual(1, b.Calls);
        }

        [Test]
        public void AddThenRemoveDuringTick_CancelsOut()
        {
            var loop = new TickLoop();
            var a = new Counter();
            var b = new Counter();
            a.OnTick = () =>
            {
                loop.Add(b);
                loop.Remove(b);
            };
            loop.Add(a);
            loop.Tick(1f);
            loop.Tick(1f);
            Assert.AreEqual(0, b.Calls);
            Assert.IsFalse(loop.Contains(b));
        }

        [Test]
        public void ThrowingItem_IsReported_AndOthersStillTick()
        {
            var loop = new TickLoop();
            var bad = new Counter { OnTick = () => throw new InvalidOperationException("boom") };
            var good = new Counter();
            ITickable failed = null;
            loop.TickFailed += (item, e) => failed = item;
            loop.Add(bad);
            loop.Add(good);
            loop.Tick(1f);
            Assert.AreSame(bad, failed);
            Assert.AreEqual(1, good.Calls);
        }
    }
}
